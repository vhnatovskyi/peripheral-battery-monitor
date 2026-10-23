using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Aj139Battery
{
    public sealed class BatteryState
    {
        public int? Percent { get; internal set; }
        public int ChargeFlag { get; internal set; }
        public bool Online { get; internal set; }
        public string Status { get; internal set; }
        public long Version { get; internal set; }
        public DateTime UpdatedUtc { get; internal set; }
    }

    // Pure parser: the native HID report includes the leading report-ID byte.
    public static class Protocol
    {
        public static byte[] Request(byte command)
        {
            if (command != 0xED && command != 0x30)
                throw new ArgumentOutOfRangeException("command");
            var packet = new byte[65];
            packet[1] = 0x55;
            packet[2] = command;
            if (command == 0xED) { packet[4] = 1; packet[5] = 0x2E; }
            else
            {
                packet[3] = 0xA5; packet[4] = 0x0B; packet[5] = 0x2E;
                packet[6] = 1; packet[7] = 1;
            }
            return packet;
        }

        public static bool TryBattery(byte[] report, int length, out int percent, out int chargeFlag)
        {
            percent = -1; chargeFlag = 0;
            if (report == null || length > report.Length || length < 11 || report[0] != 0 || report[1] != 0xAA)
                return false;
            if (report[2] == 0x30) { percent = report[9]; chargeFlag = report[10]; }
            else if (length >= 12 && report[2] == 0xFA && report[9] == 0xD0)
            { percent = report[10]; chargeFlag = report[11]; }
            return percent >= 0 && percent <= 100;
        }

        public static bool TryOnline(byte[] report, int length, out bool online)
        {
            online = false;
            if (report == null || length > report.Length || length < 10 || report[0] != 0 || report[1] != 0xAA || report[2] != 0xED)
                return false;
            online = report[9] == 2;
            return true;
        }
    }

    public sealed class Monitor : IDisposable
    {
        private readonly object sync = new object();
        private readonly SemaphoreSlim writeGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private FileStream writer;
        private Task worker;
        private int? percent;
        private int chargeFlag;
        private bool online;
        private string status = "Waiting for receiver";
        private long version;
        private DateTime updatedUtc;

        public BatteryState Snapshot()
        {
            lock (sync) return new BatteryState { Percent = percent, ChargeFlag = chargeFlag,
                Online = online, Status = status, Version = version, UpdatedUtc = updatedUtc };
        }

        public void Start()
        {
            if (worker != null) throw new InvalidOperationException("Already started");
            worker = Task.Run((Func<Task>)WatchAsync);
        }

        public void Refresh() { Task.Run((Func<Task>)RequestAsync); }

        private void Unavailable(string message)
        {
            lock (sync)
            {
                if (percent.HasValue || online || status != message) version++;
                percent = null; online = false; status = message;
            }
        }

        private void Process(byte[] report, int length)
        {
            bool connected;
            if (Protocol.TryOnline(report, length, out connected))
            {
                if (!connected) Unavailable("Mouse offline or asleep");
                else
                {
                    bool request;
                    lock (sync)
                    {
                        request = !online;
                        if (request) { online = true; status = "Waiting for battery"; version++; }
                    }
                    if (request) Refresh();
                }
                return;
            }
            int value, flag;
            if (!Protocol.TryBattery(report, length, out value, out flag)) return;
            lock (sync)
            {
                // A query reply can contain a cached value while the mouse is in BT mode.
                // An unsolicited battery notification itself proves a live mouse.
                if (!online && report[2] != 0xFA) return;
                if (percent != value || chargeFlag != flag || !online) version++;
                percent = value; chargeFlag = flag; online = true;
                status = "Connected"; updatedUtc = DateTime.UtcNow;
            }
        }

        private async Task RequestAsync()
        {
            try
            {
                await writeGate.WaitAsync(stop.Token).ConfigureAwait(false);
                try
                {
                    var output = writer;
                    if (output == null) return;
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                    {
                        timeout.CancelAfter(2500);
                        var packet = Protocol.Request(0xED);
                        await output.WriteAsync(packet, 0, packet.Length, timeout.Token).ConfigureAwait(false);
                        await Task.Delay(100, timeout.Token).ConfigureAwait(false);
                        packet = Protocol.Request(0x30);
                        await output.WriteAsync(packet, 0, packet.Length, timeout.Token).ConfigureAwait(false);
                    }
                }
                finally { writeGate.Release(); }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { Unavailable("Receiver unavailable"); }
            catch (ObjectDisposedException) { }
        }

        private async Task WatchAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        string path = Native.FindReceiver();
                        if (path == null) { Unavailable("Receiver unavailable"); }
                        else
                        {
                            using (var inputHandle = Native.Open(path))
                            using (var outputHandle = Native.Open(path))
                            {
                                if (inputHandle.IsInvalid || outputHandle.IsInvalid) throw new IOException("Cannot open receiver");
                                // Separate handles prevent a pending FileStream read from blocking writes.
                                using (var input = new FileStream(inputHandle, FileAccess.ReadWrite, 65, true))
                                using (var output = new FileStream(outputHandle, FileAccess.ReadWrite, 65, true))
                                {
                                    writer = output;
                                    var initialRequest = RequestAsync();
                                    try
                                    {
                                        var report = new byte[65];
                                        while (!stop.IsCancellationRequested)
                                        {
                                            int count = await input.ReadAsync(report, 0, report.Length, stop.Token).ConfigureAwait(false);
                                            if (count == 0) throw new IOException("Receiver disconnected");
                                            Process(report, count);
                                        }
                                    }
                                    finally
                                    {
                                        writer = null;
                                        initialRequest.GetAwaiter().GetResult();
                                    }
                                }
                            }
                        }
                    }
                    catch (IOException) { Unavailable("Receiver disconnected"); }
                    catch (UnauthorizedAccessException) { Unavailable("Receiver access denied"); }
                    // This only searches local device interfaces after a disconnect.
                    // It sends no periodic battery commands to the mouse.
                    await Task.Delay(3000, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            stop.Cancel();
            if (worker != null) worker.GetAwaiter().GetResult();
            // Refresh requests may still be completing; do not dispose their shared gate.
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct InterfaceData { public int Size; public Guid Guid; public int Flags; public IntPtr Reserved; }
        [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr window, uint flags);
        [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
        [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] private static extern int HidP_GetCaps(IntPtr data, [Out] byte[] caps);

        internal static SafeFileHandle Open(string path)
        { return CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero); }

        internal static string FindReceiver()
        {
            Guid guid; HidD_GetHidGuid(out guid);
            var set = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, 18);
            if (set == new IntPtr(-1)) return null;
            try
            {
                for (uint index = 0; ; index++)
                {
                    var data = new InterfaceData(); data.Size = Marshal.SizeOf(data);
                    if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref data)) return null;
                    uint required;
                    SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
                    if (required < 8) continue;
                    var detail = Marshal.AllocHGlobal((int)required);
                    string path;
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, required, out required, IntPtr.Zero)) continue;
                        path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                    if (path.IndexOf("vid_a8a5&pid_2255", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (var handle = Open(path))
                    {
                        if (handle.IsInvalid) continue;
                        IntPtr preparsed;
                        if (!HidD_GetPreparsedData(handle, out preparsed)) continue;
                        var caps = new byte[64]; int result;
                        try { result = HidP_GetCaps(preparsed, caps); }
                        finally { HidD_FreePreparsedData(preparsed); }
                        if (result == 0x00110000 && BitConverter.ToUInt16(caps, 0) == 0x10 &&
                            BitConverter.ToUInt16(caps, 2) == 0xFF01 && BitConverter.ToUInt16(caps, 4) == 65 &&
                            BitConverter.ToUInt16(caps, 6) == 65) return path;
                    }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }
}
