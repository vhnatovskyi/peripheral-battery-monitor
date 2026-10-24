using System;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Aj139Battery
{
    [ComImport, Guid("905a0fe0-bc53-11df-8c49-001e4fc686da"), InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    internal interface IWindowsBuffer { uint Capacity { get; } uint Length { get; set; } }
    [ComImport, Guid("905a0fef-bc53-11df-8c49-001e4fc686da"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IWindowsBufferBytes { void Buffer(out IntPtr data); }
    public static class BluetoothBuffer
    {
        public static int? Battery(object value)
        {
            var buffer = (IWindowsBuffer)value;
            if (buffer.Length != 1) return null;
            IntPtr data; ((IWindowsBufferBytes)value).Buffer(out data);
            byte level = Marshal.ReadByte(data);
            return level <= 100 ? (int?)level : null;
        }
    }
    // Bind WinRT's token-based event directly; PowerShell cannot subscribe to it.
    public sealed class BluetoothBatterySubscription : IDisposable
    {
        private readonly object sync = new object();
        private readonly object characteristic;
        private readonly EventInfo changed;
        private readonly Delegate handler;
        private readonly object token;
        private int? percent;
        private DateTime updated;
        public int? Percent { get { lock (sync) return percent; } }
        public DateTime UpdatedUtc { get { lock (sync) return updated; } }
        public BluetoothBatterySubscription(object value, int? initial)
        {
            characteristic = value; percent = initial; updated = DateTime.UtcNow;
            changed = value.GetType().GetEvent("ValueChanged");
            handler = Delegate.CreateDelegate(changed.EventHandlerType, this, GetType().GetMethod("OnChanged", BindingFlags.NonPublic | BindingFlags.Instance));
            token = changed.GetAddMethod().Invoke(value, new object[] { handler });
        }
        private void OnChanged(object sender, object args)
        {
            var buffer = args.GetType().GetProperty("CharacteristicValue").GetValue(args, null);
            var level = BluetoothBuffer.Battery(buffer);
            if (!level.HasValue) return;
            lock (sync) { percent = level; updated = DateTime.UtcNow; }
        }
        public void Dispose() { changed.GetRemoveMethod().Invoke(characteristic, new object[] { token }); }
    }
}
