using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Aj139Battery
{
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

        internal static SafeFileHandle OpenQuery(string path)
        { return CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero); }

        internal static string FindReceiver()
        { return FindInterface("vid_a8a5&pid_2255", 0xFF01, 0x10, 65, 65); }

        internal static string FindMagicKeyboard()
        { return FindInterface("vid&0001004c_pid&029f", 0xFF00, 0x14, 3, 0); }

        private static string FindInterface(string identity, ushort page, ushort usage, ushort inputSize, ushort outputSize)
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
                    if (path.IndexOf(identity, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (var handle = OpenQuery(path))
                    {
                        if (handle.IsInvalid) continue;
                        IntPtr preparsed;
                        if (!HidD_GetPreparsedData(handle, out preparsed)) continue;
                        var caps = new byte[64]; int result;
                        try { result = HidP_GetCaps(preparsed, caps); }
                        finally { HidD_FreePreparsedData(preparsed); }
                        if (result == 0x00110000 && BitConverter.ToUInt16(caps, 0) == usage &&
                            BitConverter.ToUInt16(caps, 2) == page && BitConverter.ToUInt16(caps, 4) == inputSize &&
                            BitConverter.ToUInt16(caps, 6) == outputSize) return path;
                    }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
        }
    }
}
