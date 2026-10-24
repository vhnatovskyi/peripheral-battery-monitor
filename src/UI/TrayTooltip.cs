using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PeripheralBatteryMonitor.UI
{
    public static class TrayTooltip
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct IconData
        {
            public int Size; public IntPtr Window; public uint Id, Flags, Message; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
            public uint State, StateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
            public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint message, ref IconData data);
        // Framework's public Text setter is limited to 63 characters. Windows
        // supports 127, allowing four compact, separate device-status lines.
        public static bool Set(NotifyIcon icon, string text)
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var type = typeof(NotifyIcon);
            var windowField = type.GetField("window", flags) ?? type.GetField("_window", flags);
            var idField = type.GetField("id", flags) ?? type.GetField("_id", flags);
            if (windowField == null || idField == null) return false;
            var window = windowField.GetValue(icon) as NativeWindow;
            if (window == null || window.Handle == IntPtr.Zero) return false;
            var data = new IconData { Size = Marshal.SizeOf(typeof(IconData)), Window = window.Handle,
                Id = Convert.ToUInt32(idField.GetValue(icon)), Flags = 4,
                Tip = text.Length <= 127 ? text : text.Substring(0, 127) };
            return Shell_NotifyIcon(1, ref data);
        }
    }
}
