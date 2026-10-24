using System.Drawing;
using System.Runtime.InteropServices;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

internal readonly record struct ScreenArea(int X,int Y,int Width,int Height);
internal static class NativeDisplay
{
    [StructLayout(LayoutKind.Sequential)]private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo { public int Size;public Rect Monitor,Work;public uint Flags; }
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromRect(ref Rect rect,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    public static ScreenArea ForWindow(IntPtr window)=>Read(MonitorFromWindow(window,2));
    public static ScreenArea ForRect(Rectangle rectangle)
    { var rect=new Rect { Left=rectangle.Left,Top=rectangle.Top,Right=rectangle.Right,Bottom=rectangle.Bottom };return Read(MonitorFromRect(ref rect,2)); }
    private static ScreenArea Read(IntPtr monitor)
    {
        var info=new MonitorInfo { Size=Marshal.SizeOf<MonitorInfo>() };
        if(!GetMonitorInfo(monitor,ref info))throw new System.ComponentModel.Win32Exception();
        return new(info.Work.Left,info.Work.Top,info.Work.Right-info.Work.Left,info.Work.Bottom-info.Work.Top);
    }
}
