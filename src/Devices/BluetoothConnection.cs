using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Threading.Tasks;

namespace Aj139Battery
{
    public static class BluetoothConnection
    {
        [StructLayout(LayoutKind.Sequential)] private struct RadioSearch { public int Size; }
        [DllImport("BluetoothAPIs.dll", SetLastError=true)] private static extern IntPtr BluetoothFindFirstRadio(ref RadioSearch search, out SafeFileHandle radio);
        [DllImport("BluetoothAPIs.dll", SetLastError=true)] private static extern bool BluetoothFindNextRadio(IntPtr search, out SafeFileHandle radio);
        [DllImport("BluetoothAPIs.dll")] private static extern bool BluetoothFindRadioClose(IntPtr search);
        [DllImport("kernel32.dll", SetLastError=true)] private static extern bool DeviceIoControl(SafeFileHandle device, uint code, ref ulong input, uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
        public static ulong ParseAddress(string address)
        {
            string normalized = (address ?? "").Replace(":", "").Replace("-", "");
            ulong value;
            if(normalized.Length != 12 || !UInt64.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value) || value == 0)
                throw new ArgumentException("Invalid Bluetooth device address.");
            return value;
        }
        // Bthioctl.h: BTH_CTL(3), FILE_DEVICE_BLUETOOTH=0x41.
        // Disconnects only the specified remote address; retains pairing.
        public static int Disconnect(string address)
        {
            ulong target = ParseAddress(address);
            var search = new RadioSearch { Size = Marshal.SizeOf(typeof(RadioSearch)) };
            SafeFileHandle radio; IntPtr finder = BluetoothFindFirstRadio(ref search, out radio);
            if(finder == IntPtr.Zero) return Marshal.GetLastWin32Error();
            int error = 1167;
            try
            {
                do
                {
                    using(radio)
                    {
                        uint returned;
                        if(DeviceIoControl(radio, 0x0041000c, ref target, 8, IntPtr.Zero, 0, out returned, IntPtr.Zero)) return 0;
                        error = Marshal.GetLastWin32Error();
                    }
                } while(BluetoothFindNextRadio(finder, out radio));
            }
            finally { BluetoothFindRadioClose(finder); }
            return error;
        }
        public static Task<int> DisconnectAsync(string address)
        { ParseAddress(address); return Task.Run(() => Disconnect(address)); }
    }
}
