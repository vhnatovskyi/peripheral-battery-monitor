using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Aj139Battery
{
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


}
