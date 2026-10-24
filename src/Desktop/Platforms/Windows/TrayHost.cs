using System.Runtime.InteropServices;
using System.Drawing;
using PeripheralBatteryMonitor.Desktop.Services;
using PeripheralBatteryMonitor.UI;
using Color=System.Drawing.Color;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

public sealed class TrayHost : IDisposable
{
    private const uint Callback=0x8000+815;
    private readonly IntPtr window;
    private readonly SubclassProc callback;
    private readonly Action<string> action;
    private readonly Dictionary<uint,IntPtr> icons=[];
    private string fingerprint="";
    private MonitorSettings settings=new();
    private bool lightTaskbar=IsLightTaskbar();
    private static bool IsLightTaskbar(){try{return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","SystemUsesLightTheme",0) is int mode && mode==1;}catch{return false;}}
    private readonly uint taskbarCreated=RegisterWindowMessage("TaskbarCreated");
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct IconData
    {
        public int Size;public IntPtr Window;public uint Id,Flags,Message;public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;
        public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Title;
        public uint InfoFlags;public Guid Guid;public IntPtr BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)]private struct Point { public int X,Y; }
    private delegate IntPtr SubclassProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam,UIntPtr id,IntPtr data);
    [DllImport("comctl32.dll")]private static extern bool SetWindowSubclass(IntPtr hwnd,SubclassProc callback,UIntPtr id,IntPtr data);
    [DllImport("comctl32.dll")]private static extern bool RemoveWindowSubclass(IntPtr hwnd,SubclassProc callback,UIntPtr id);
    [DllImport("comctl32.dll")]private static extern IntPtr DefSubclassProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern bool Shell_NotifyIcon(uint operation,ref IconData data);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")]private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")]private static extern int GetSystemMetricsForDpi(int index,uint dpi);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll")]private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr id,string? label);
    [DllImport("user32.dll")]private static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr hwnd,IntPtr data);
    [DllImport("user32.dll")]private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")]private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr hwnd);
    public TrayHost(Microsoft.UI.Xaml.Window nativeWindow,Action<string> onAction)
    { window=WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);action=onAction;callback=WindowMessage;SetWindowSubclass(window,callback,new UIntPtr(815),IntPtr.Zero); }
    public void Invalidate()=>fingerprint="";
    public void Update(IReadOnlyList<DeviceReading> readings,MonitorSettings current)
    {
        settings=current;
        string key=current.TrayMode+current.TrayStyle+current.Language+string.Join('|',readings.Select(d=>$"{d.Id}:{d.Percent}:{d.Accent}:{d.Transport}"));
        if(key==fingerprint)return;fingerprint=key;
        bool combined=current.TrayMode!="Separate" || readings.Count==0;
        var groups=combined ? new[]{readings.Take(4).ToArray()} : readings.Select(d=>new[]{d}).ToArray();
        var used=new HashSet<uint>();
        for(int i=0;i<groups.Length;i++)
        {
            uint id=(uint)i+1;used.Add(id);var group=groups[i];
            using var bitmap=TrayBatteryPainter.Render(group.Select(d=>d.Percent ?? -1).ToArray(),group.Select(d=>{var color=ColorTranslator.FromHtml(d.Accent);return lightTaskbar && !combined ? Color.FromArgb((int)(color.R*.62),(int)(color.G*.62),(int)(color.B*.62)):color;}).ToArray(),combined,current.TrayStyle=="Percent",Math.Max(16,GetSystemMetricsForDpi(49,GetDpiForWindow(window))),lightTaskbar);
            IntPtr image=bitmap.GetHicon();
            var data=Data(id);data.Flags=1|2|4;data.Message=Callback;data.Icon=image;data.Tip=Tooltip(group,current.Language);
            Shell_NotifyIcon(icons.ContainsKey(id) ? 1u : 0u,ref data);
            if(icons.TryGetValue(id,out var previous))DestroyIcon(previous);icons[id]=image;
        }
        foreach(uint id in icons.Keys.Where(id=>!used.Contains(id)).ToArray())Remove(id);
    }
    private IconData Data(uint id)=>new(){Size=Marshal.SizeOf<IconData>(),Window=window,Id=id,Tip="",Info="",Title=""};
    private static string Tooltip(DeviceReading[] devices,string language)
    {
        if(devices.Length==0)return language=="uk" ? "Немає підключених пристроїв" : "No devices connected";
        return string.Join('\n',devices.Select(d=>
        {
            string tail=$": {(d.Percent.HasValue ? d.Percent+"%" : "--")} / {(d.Transport=="Bluetooth" ? "BT" : d.Transport.Contains("2.4") ? "2.4G" : "USB")}";
            int available=31-tail.Length;string name=d.Name.Length>available ? d.Name[..(available-1)]+"…" : d.Name;return name+tail;
        }));
    }
    private IntPtr WindowMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam,UIntPtr id,IntPtr data)
    {
        if(message==taskbarCreated){foreach(var image in icons.Values)DestroyIcon(image);icons.Clear();Invalidate();}
        if(message is 0x1A or 0x31A){lightTaskbar=IsLightTaskbar();Invalidate();}
        if(message==Callback)
        {
            int mouse=(int)lParam.ToInt64();
            if(mouse is 0x202 or 0x203)action("devices");
            if(mouse==0x205)ShowMenu();
            return IntPtr.Zero;
        }
        return DefSubclassProc(hwnd,message,wParam,lParam);
    }
    private void ShowMenu()
    {
        IntPtr menu=CreatePopupMenu();string[] commands=["devices","refresh","settings","releases","about","exit"];
        string[] labels=settings.Language=="uk" ? ["Відкрити","Оновити заряд","Налаштування","Релізи","Про програму","Вийти"] : ["Open","Refresh batteries","Settings","Releases","About","Exit"];
        try
        {
            for(int i=0;i<labels.Length;i++)AppendMenu(menu,0,new UIntPtr((uint)i+1),labels[i]);
            GetCursorPos(out var point);SetForegroundWindow(window);uint choice=TrackPopupMenuEx(menu,0x100|2,point.X,point.Y,window,IntPtr.Zero);
            if(choice>0 && choice<=commands.Length)action(commands[choice-1]);
        }finally{DestroyMenu(menu);}
    }
    public void Notify(string title,string message)
    { if(icons.Count==0)return;var data=Data(icons.Keys.First());data.Flags=16;data.Title=title[..Math.Min(63,title.Length)];data.Info=message[..Math.Min(255,message.Length)];data.InfoFlags=1;Shell_NotifyIcon(1,ref data); }
    private void Remove(uint id){var data=Data(id);Shell_NotifyIcon(2,ref data);DestroyIcon(icons[id]);icons.Remove(id);}
    public void Dispose(){foreach(uint id in icons.Keys.ToArray())Remove(id);RemoveWindowSubclass(window,callback,new UIntPtr(815));}
}
