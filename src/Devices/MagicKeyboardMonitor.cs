using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Aj139Battery
{
    public static class MagicKeyboardProtocol
    {
        // Apple vendor collection FF00/0014. Unlike AJ139, byte 0 is report ID 90.
        public static bool TryBattery(byte[] report, int length, out int percent)
        {
            percent = -1;
            if (report == null || length != 3 || report.Length < length || report[0] != 0x90 || report[2] > 100)
                return false;
            percent = report[2];
            return true;
        }
    }

    public sealed class MagicKeyboardMonitor : IDisposable
    {
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetInputReport(SafeFileHandle handle, byte[] report, int length);
        private readonly object sync = new object();
        private readonly SemaphoreSlim queryGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private Task worker;
        private int? percent;
        private long version;
        private string status = "Waiting for Magic Keyboard";
        private DateTime updatedUtc;
        private bool? connected;
        private long generation;

        public void SetConnection(bool? value)
        {
            if (!value.HasValue) return;
            lock (sync)
            {
                if (connected == value) return;
                connected = value;
                generation++;
                if (value == false) Unavailable();
            }
        }

        public BatteryState Snapshot()
        {
            lock (sync) return new BatteryState { Percent = percent, Online = percent.HasValue,
                Status = status, Version = version, UpdatedUtc = updatedUtc };
        }

        private void Process(byte[] report, int length)
        {
            int value;
            if (!MagicKeyboardProtocol.TryBattery(report, length, out value)) return;
            lock (sync)
            {
                if (connected == false) return;
                if (percent != value) version++;
                percent = value; status = "Connected"; updatedUtc = DateTime.UtcNow;
            }
        }

        private void Unavailable()
        {
            lock (sync)
            {
                if (percent.HasValue || status != "Keyboard unavailable") version++;
                percent = null; status = "Keyboard unavailable";
            }
        }

        public void Start()
        {
            if (worker != null) throw new InvalidOperationException("Already started");
            worker = Task.Run((Func<Task>)WatchAsync);
        }

        public void Refresh() { Task.Run((Func<Task>)ReadCurrentAsync); }

        private async Task ReadCurrentAsync()
        {
            try
            {
                if (!await queryGate.WaitAsync(0, stop.Token).ConfigureAwait(false)) return;
                try
                {
                    long requestGeneration;
                    lock (sync) { if (connected == false) return; requestGeneration = generation; }
                    var path = Native.FindMagicKeyboard();
                    if (path == null) { Unavailable(); return; }
                    using (var handle = Native.OpenQuery(path))
                    {
                        var report = new byte[3]; report[0] = 0x90;
                        bool success = !handle.IsInvalid && HidD_GetInputReport(handle, report, report.Length);
                        lock (sync)
                        {
                            if (generation != requestGeneration) return;
                            if (success) Process(report, report.Length);
                            else Unavailable();
                        }
                    }
                }
                finally { queryGate.Release(); }
            }
            catch (OperationCanceledException) { }
        }

        private async Task WatchAsync()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    try
                    {
                        long sessionGeneration;
                        lock (sync) sessionGeneration = generation;
                        var path = Native.FindMagicKeyboard();
                        bool disconnected;
                        lock (sync) disconnected = connected == false;
                        if (path == null || disconnected) Unavailable();
                        else using (var handle = Native.Open(path))
                        {
                            if (handle.IsInvalid) throw new IOException("Keyboard unavailable");
                            using (var input = new FileStream(handle, FileAccess.ReadWrite, 3, true))
                            using (var session = CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
                            {
                                // This is a battery-only vendor collection, not the key input collection.
                                var report = new byte[3];
                                var read = input.ReadAsync(report, 0, report.Length, session.Token);
                                try
                                {
                                await ReadCurrentAsync().ConfigureAwait(false);
                                while (!stop.IsCancellationRequested)
                                {
                                    await Task.WhenAny(read, Task.Delay(3000, session.Token)).ConfigureAwait(false);
                                    lock (sync) disconnected = connected == false || generation != sessionGeneration;
                                    if (disconnected) break;
                                    if (!read.IsCompleted)
                                    {
                                        // Reconnection may precede the HID collection becoming ready.
                                        // Retry only until an initial battery report is available.
                                        if (!Snapshot().Online) await ReadCurrentAsync().ConfigureAwait(false);
                                        continue;
                                    }
                                    int count = await read.ConfigureAwait(false);
                                    if (count == 0) throw new IOException("Keyboard disconnected");
                                    lock (sync) { if (generation == sessionGeneration) Process(report, count); }
                                    read = input.ReadAsync(report, 0, report.Length, session.Token);
                                }
                                }
                                finally
                                {
                                    session.Cancel();
                                    try { read.GetAwaiter().GetResult(); }
                                    catch (OperationCanceledException) { }
                                    catch (IOException) { }
                                }
                            }
                        }
                    }
                    catch (IOException) { Unavailable(); }
                    catch (UnauthorizedAccessException) { Unavailable(); }
                    // Retry local device discovery only. No timed battery requests.
                    await Task.Delay(3000, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            stop.Cancel();
            if (worker != null) worker.GetAwaiter().GetResult();
        }
    }
}
