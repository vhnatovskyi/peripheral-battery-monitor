using System.Runtime.InteropServices;

namespace PeripheralBatteryMonitor.Agent;

internal sealed class NativeHostWindow:IDisposable
{
    internal const uint Dispatch=0x8000+91;
    private readonly Procedure callback;
    private readonly string className="PbmBackground"+Guid.NewGuid().ToString("N");
    private readonly IntPtr module=GetModuleHandle(null);
    public IntPtr Handle { get; }
    public event Action<uint>? Message;
    public NativeHostWindow()
    {
        callback=WindowMessage;
        var type=new WindowClass { Size=(uint)Marshal.SizeOf<WindowClass>(),Module=module,Name=className,Procedure=Marshal.GetFunctionPointerForDelegate(callback) };
        if(RegisterClassEx(ref type)==0)throw new System.ComponentModel.Win32Exception();
        Handle=CreateWindowEx(0x80,className,"Peripheral Battery Monitor Background",0,0,0,0,0,IntPtr.Zero,IntPtr.Zero,module,IntPtr.Zero);
        if(Handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();
        SetTimer(Handle,new UIntPtr(1),100,IntPtr.Zero);
    }
    private IntPtr WindowMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam)
    {
        if(message is 0x113 or Dispatch or 0x1A or 0x31A)Message?.Invoke(message);
        return DefWindowProc(window,message,wParam,lParam);
    }
    public void Wake()=>PostMessage(Handle,Dispatch,IntPtr.Zero,IntPtr.Zero);
    public void Run()
    {
        int result;
        while((result=GetMessage(out var message,IntPtr.Zero,0,0))>0){TranslateMessage(ref message);DispatchMessage(ref message);}
        if(result<0)throw new System.ComponentModel.Win32Exception();
    }
    public void Quit()=>PostQuitMessage(0);
    public void Dispose(){KillTimer(Handle,new UIntPtr(1));DestroyWindow(Handle);UnregisterClass(className,module);}
    private delegate IntPtr Procedure(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WindowClass { public uint Size,Style;public IntPtr Procedure;public int ClassExtra,WindowExtra;public IntPtr Module,Icon,Cursor,Background;public string? Menu,Name;public IntPtr SmallIcon; }
    [StructLayout(LayoutKind.Sequential)]private struct NativeMessage { public IntPtr Window;public uint Id;public UIntPtr WParam;public IntPtr LParam;public uint Time;public int X,Y;public uint Private; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern ushort RegisterClassEx(ref WindowClass value);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool UnregisterClass(string name,IntPtr module);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateWindowEx(uint ex,string name,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr module,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr DefWindowProc(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern UIntPtr SetTimer(IntPtr window,UIntPtr id,uint milliseconds,IntPtr callback);
    [DllImport("user32.dll")]private static extern bool KillTimer(IntPtr window,UIntPtr id);
    [DllImport("user32.dll")]private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetMessage(out NativeMessage message,IntPtr window,uint min,uint max);
    [DllImport("user32.dll")]private static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr DispatchMessage(ref NativeMessage message);
    [DllImport("user32.dll")]private static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern void PostQuitMessage(int code);
}
