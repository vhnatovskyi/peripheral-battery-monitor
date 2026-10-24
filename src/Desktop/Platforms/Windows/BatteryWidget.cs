using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Windowing;
using PeripheralBatteryMonitor.Desktop.Services;
using Color=System.Drawing.Color;
using Font=System.Drawing.Font;
using Point=System.Drawing.Point;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

// Per-pixel alpha avoids WinUI's opaque window backing surface. Text and bars
// stay opaque; a dedicated borderless popup has no system frame to turn white.
internal sealed class BatteryWidget : IDisposable
{
    private readonly IntPtr handle,owner,module;
    private readonly WindowProc procedure;
    private readonly string className="PbmBatteryWidget"+Guid.NewGuid().ToString("N");
    private readonly bool persist;
    private readonly Microsoft.UI.Xaml.DispatcherTimer guard=new() { Interval=TimeSpan.FromMilliseconds(100) };
    private IReadOnlyList<DeviceReading> readings=[];
    private Bitmap? surface;
    private string fingerprint="";
    private bool disposed,positionDirty;
    private DateTime moved;
    private uint dpi;
    internal bool IsMoving { get; private set; }
    internal int DeviceCount { get; private set; }
    internal bool ClickThrough=>(GetWindowLong(handle,-20)&0x20)!=0;
    internal bool IsAlwaysOnTop=>(GetWindowLong(handle,-20)&8)!=0;
    internal bool HasBorder=>(GetWindowLong(handle,-16)&0x00C40000)!=0;
    internal byte BackgroundOpacity=>(byte)Math.Round(Math.Clamp(AppRuntime.Current.Settings.Value.WidgetOpacity,0,100)*2.55);
    internal Rectangle Bounds { get { GetWindowRect(handle,out var r);return new(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top); } }

    public BatteryWidget(Microsoft.UI.Xaml.Window main,bool persistPosition=true)
    {
        persist=persistPosition;owner=WinRT.Interop.WindowNative.GetWindowHandle(main);
        module=GetModuleHandle(null);procedure=WindowMessage;
        var wc=new WindowClass { Size=(uint)Marshal.SizeOf<WindowClass>(),Instance=module,Procedure=Marshal.GetFunctionPointerForDelegate(procedure),Name=className,Cursor=LoadCursor(IntPtr.Zero,new IntPtr(32512)) };
        if(RegisterClassEx(ref wc)==0)throw new Win32Exception(Marshal.GetLastWin32Error());
        handle=CreateWindowEx(0x080800A8,className,"Peripheral Battery Monitor — Widget",0x80000000,0,0,280,124,IntPtr.Zero,IntPtr.Zero,module,IntPtr.Zero);
        if(handle==IntPtr.Zero){UnregisterClass(className,module);throw new Win32Exception(Marshal.GetLastWin32Error());}
        uint noBorder=0xFFFFFFFE;DwmSetWindowAttribute(handle,34,ref noBorder,4);
        RestorePosition(main);Update([]);
        guard.Tick+=(_,_)=>
        {
            CheckInteractionFocus(GetForegroundWindow()==owner || GetForegroundWindow()==handle);
            if(positionDirty && DateTime.UtcNow-moved>TimeSpan.FromMilliseconds(600))SavePosition();
            if(dpi!=GetDpiForWindow(handle)){fingerprint="";Update(readings);}
        };
        guard.Start();ShowWindow(handle,4);
    }
    internal static bool CanMove(bool monitorHasFocus,bool armed)=>monitorHasFocus && armed;
    internal void CheckInteractionFocus(bool monitorHasFocus){if(IsMoving && !monitorHasFocus)SetMoving(false);}
    public void BeginMove(){if(GetForegroundWindow()==owner)SetMoving(true);}
    internal void SetMoving(bool value)
    {
        IsMoving=value;int style=GetWindowLong(handle,-20);
        SetWindowLong(handle,-20,value?style&~0x20:style|0x20);
        SetWindowPos(handle,IntPtr.Zero,0,0,0,0,0x37);
    }
    private IntPtr WindowMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam)
    {
        if(message==0x21)return new IntPtr(3); // MA_NOACTIVATE.
        if(message==0x10)return IntPtr.Zero;
        if(message==0x20 && IsMoving){SetCursor(LoadCursor(IntPtr.Zero,new IntPtr(32646)));return new IntPtr(1);}
        if(message==0x47 && handle!=IntPtr.Zero){positionDirty=true;moved=DateTime.UtcNow;}
        if(message==0x201)
        {
            if(!CanMove(GetForegroundWindow()==owner || GetForegroundWindow()==handle,IsMoving)){SetMoving(false);return IntPtr.Zero;}
            ReleaseCapture();SendMessage(hwnd,0xA1,new IntPtr(2),IntPtr.Zero);return IntPtr.Zero;
        }
        return DefWindowProc(hwnd,message,wParam,lParam);
    }
    public void Update(IReadOnlyList<DeviceReading> value)
    {
        if(disposed)return;readings=value;
        value=value.Where(d=>AppRuntime.Current.Settings.Value.ShowInWidget(d.Id)).ToArray();
        bool dark=Views.Design.IsDark;
        bool minimized=AppRuntime.Current.Settings.Value.WidgetMode=="Minimized" && value.Count>0;
        bool vertical=AppRuntime.Current.Settings.Value.WidgetOrientation=="Vertical";
        string next=$"{dark}|{BackgroundOpacity}|{minimized}|{vertical}|{readings.Count}|{AppRuntime.Current.Settings.Value.Language}|"+string.Join('|',value.Select(d=>$"{d.Id}:{d.Name}:{d.Percent}:{d.Accent}"));
        if(fingerprint==next)return;
        dpi=GetDpiForWindow(handle);double scale=dpi/96d;
        var old=Bounds;
        var area=DisplayArea.GetFromRect(new(old.X,old.Y,old.Width,old.Height),DisplayAreaFallback.Nearest).WorkArea;
        int columns=vertical?1:Math.Min(4,Math.Max(1,value.Count));
        int width=(int)Math.Ceiling((minimized?12+columns*84:280)*scale);
        int height=Math.Min(area.Height-16,(int)Math.Ceiling((minimized?12+((value.Count+columns-1)/columns)*32:10+Math.Max(1,value.Count)*38)*scale));
        height=Math.Max(40,height);
        // Rasterize text at four times the target density, then reduce with a
        // premultiplied-alpha filter. ClearType is unsuitable on transparent pixels.
        const int sampling=4;
        using var highResolution=new Bitmap(width*sampling,height*sampling,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(highResolution))
        {
            g.ScaleTransform(sampling,sampling);
            g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.CompositingMode=CompositingMode.SourceCopy;
            using var panel=Rounded(width-1,height-1,(float)(10*scale));
            using var fill=new SolidBrush(Color.FromArgb(BackgroundOpacity,ColorTranslator.FromHtml(dark?"#172232":"#F5F8FC")));
            g.FillPath(fill,panel);g.CompositingMode=CompositingMode.SourceOver;g.TextRenderingHint=TextRenderingHint.AntiAlias;
            if(minimized)DrawMinimized(g,value,scale,dark,columns);
            else
            {
            using var nameFont=new Font("Segoe UI",(float)(12.5*scale),FontStyle.Regular,GraphicsUnit.Pixel);
            using var chargeFont=new Font("Segoe UI",(float)(18*scale),FontStyle.Bold,GraphicsUnit.Pixel);
            using var text=new SolidBrush(ColorTranslator.FromHtml(dark?"#F3F6FA":"#182435"));
            using var track=new SolidBrush(ColorTranslator.FromHtml(dark?"#34465D":"#DCE5F0"));
            using var names=new StringFormat { Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap,LineAlignment=StringAlignment.Center };
            using var charges=new StringFormat { Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap };
            if(value.Count==0)g.DrawString(readings.Count==0?AppRuntime.Current.Text("Немає підключених пристроїв","No connected devices"):AppRuntime.Current.Text("Усі пристрої приховані","All devices hidden"),nameFont,text,new RectangleF((float)(10*scale),0,width-(float)(20*scale),height),names);
            for(int i=0;i<value.Count;i++)
            {
                float top=(float)((5+i*38)*scale);if(top+29*scale>height)break;
                var device=value[i];var accent=ColorTranslator.FromHtml(device.Accent);
                using var color=new SolidBrush(accent);
                using var charge=new SolidBrush(dark?accent:Color.FromArgb((int)(accent.R*0.55),(int)(accent.G*0.55),(int)(accent.B*0.55)));
                g.DrawString(device.Name,nameFont,text,new RectangleF((float)(10*scale),top,width-(float)(82*scale),(float)(25*scale)),names);
                g.DrawString(device.Percent is int p?$"{p}%":"—",chargeFont,charge,new RectangleF(width-(float)(74*scale),top,(float)(64*scale),(float)(25*scale)),charges);
                var bar=new RectangleF((float)(10*scale),top+(float)(28*scale),width-(float)(20*scale),(float)(2.5*scale));
                g.FillRectangle(track,bar);bar.Width*=Math.Clamp(device.Percent??0,0,100)/100f;g.FillRectangle(color,bar);
            }
            }
        }
        var bitmap=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        using(var output=Graphics.FromImage(bitmap))
        using(var edges=new ImageAttributes())
        {
            output.CompositingMode=CompositingMode.SourceCopy;output.InterpolationMode=InterpolationMode.HighQualityBicubic;output.PixelOffsetMode=PixelOffsetMode.HighQuality;
            edges.SetWrapMode(WrapMode.TileFlipXY);
            output.DrawImage(highResolution,new Rectangle(0,0,width,height),0,0,highResolution.Width,highResolution.Height,GraphicsUnit.Pixel,edges);
        }
        try { Present(bitmap); } catch { bitmap.Dispose();throw; }
        surface?.Dispose();surface=bitmap;DeviceCount=value.Count;fingerprint=next;KeepOnDisplay(area);
    }
    private static void DrawMinimized(Graphics g,IReadOnlyList<DeviceReading> devices,double scale,bool dark,int columns)
    {
        using var symbolFont=new Font(Views.Design.IconFont,(float)(18*scale),FontStyle.Regular,GraphicsUnit.Pixel);
        using var percentFont=new Font("Segoe UI",(float)(12*scale),FontStyle.Bold,GraphicsUnit.Pixel);
        using var text=new SolidBrush(ColorTranslator.FromHtml(dark?"#F3F6FA":"#182435"));
        using var track=new SolidBrush(ColorTranslator.FromHtml(dark?"#243449":"#E3EBF4"));
        using var centered=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.NoWrap };
        for(int i=0;i<devices.Count;i++)
        {
            var device=devices[i];float x=(float)((6+i%columns*84)*scale),y=(float)((6+i/columns*32)*scale);
            var accent=ColorTranslator.FromHtml(device.Accent);
            if(!dark)accent=Color.FromArgb((int)(accent.R*.62),(int)(accent.G*.62),(int)(accent.B*.62));
            using var color=new SolidBrush(accent);
            using var tint=new SolidBrush(Color.FromArgb(75,accent));
            using var outline=new Pen(accent,(float)(1.5*scale));
            g.DrawString(Views.Design.Glyph(device.Kind),symbolFont,color,new RectangleF(x,y+(float)(3*scale),(float)(22*scale),(float)(24*scale)),centered);
            var battery=new RectangleF(x+(float)(27*scale),y+(float)(4*scale),(float)(46*scale),(float)(22*scale));
            g.FillRectangle(track,battery);
            var level=battery;level.Width*=Math.Clamp(device.Percent??0,0,100)/100f;g.FillRectangle(tint,level);
            g.DrawRectangle(outline,battery.X,battery.Y,battery.Width,battery.Height);
            g.FillRectangle(color,battery.Right,battery.Y+(float)(7*scale),(float)(2.5*scale),(float)(8*scale));
            g.DrawString(device.Percent is int percent?$"{percent}%":"—",percentFont,text,battery,centered);
        }
    }
    private void Present(Bitmap bitmap)
    {
        IntPtr screen=GetDC(IntPtr.Zero),dc=IntPtr.Zero,image=IntPtr.Zero,previous=IntPtr.Zero;
        try
        {
            dc=CreateCompatibleDC(screen);image=bitmap.GetHbitmap(Color.FromArgb(0));previous=SelectObject(dc,image);
            var p=Bounds.Location;var destination=new NativePoint(p.X,p.Y);var source=new NativePoint(0,0);var size=new NativeSize(bitmap.Width,bitmap.Height);
            var blend=new Blend { ConstantAlpha=255,AlphaFormat=1 };
            if(!UpdateLayeredWindow(handle,screen,ref destination,ref size,dc,ref source,0,ref blend,2))throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { if(previous!=IntPtr.Zero)SelectObject(dc,previous);if(image!=IntPtr.Zero)DeleteObject(image);if(dc!=IntPtr.Zero)DeleteDC(dc);if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen); }
    }
    internal Bitmap RenderPreview()=>(Bitmap)surface!.Clone();
    internal void MoveTo(int x,int y)=>SetWindowPos(handle,IntPtr.Zero,x,y,0,0,0x15);
    internal void Raise()=>SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x13);
    private static GraphicsPath Rounded(float width,float height,float radius)
    {
        var path=new GraphicsPath();float d=radius*2;
        path.AddArc(0,0,d,d,180,90);path.AddArc(width-d,0,d,d,270,90);path.AddArc(width-d,height-d,d,d,0,90);path.AddArc(0,height-d,d,d,90,90);path.CloseFigure();return path;
    }
    private void RestorePosition(Microsoft.UI.Xaml.Window main)
    {
        var area=DisplayArea.GetFromWindowId(main.AppWindow.Id,DisplayAreaFallback.Nearest).WorkArea;
        var position=new WidgetPosition { X=area.X+area.Width-300,Y=area.Y+24 };
        if(persist)try { position=JsonSerializer.Deserialize(File.ReadAllText(PositionPath),AppJsonContext.Default.WidgetPosition)??position; }
        catch(Exception error) when(error is IOException or JsonException or UnauthorizedAccessException) { }
        area=DisplayArea.GetFromRect(new(position.X,position.Y,280,124),DisplayAreaFallback.Nearest).WorkArea;
        MoveTo(position.X,position.Y);KeepOnDisplay(area);
    }
    private void KeepOnDisplay(global::Windows.Graphics.RectInt32 area)
    {
        var r=Bounds;int x=Math.Clamp(r.X,area.X,Math.Max(area.X,area.X+area.Width-r.Width)),y=Math.Clamp(r.Y,area.Y,Math.Max(area.Y,area.Y+area.Height-r.Height));
        if(x!=r.X || y!=r.Y)MoveTo(x,y);
    }
    private static string PositionPath=>Path.Combine(SettingsStore.StateRoot,"widget-position.json");
    private void SavePosition()
    {
        positionDirty=false;if(!persist)return;
        try
        {
            var p=Bounds.Location;Directory.CreateDirectory(SettingsStore.StateRoot);
            File.WriteAllText(PositionPath+".pending",JsonSerializer.Serialize(new WidgetPosition { X=p.X,Y=p.Y },AppJsonContext.Default.WidgetPosition));
            File.Move(PositionPath+".pending",PositionPath,true);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
    }
    public void Dispose(){if(disposed)return;SavePosition();disposed=true;guard.Stop();DestroyWindow(handle);UnregisterClass(className,module);surface?.Dispose();}
    private delegate IntPtr WindowProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct WindowClass { public uint Size,Style;public IntPtr Procedure;public int ClassExtra,WindowExtra;public IntPtr Instance,Icon,Cursor,Background;public string? Menu,Name;public IntPtr SmallIcon; }
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)]private struct NativePoint(int x,int y) { public int X=x,Y=y; }
    [StructLayout(LayoutKind.Sequential)]private struct NativeSize(int width,int height) { public int Width=width,Height=height; }
    [StructLayout(LayoutKind.Sequential,Pack=1)]private struct Blend { public byte Operation,Flags,ConstantAlpha,AlphaFormat; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern ushort RegisterClassEx(ref WindowClass wc);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool UnregisterClass(string name,IntPtr instance);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr CreateWindowEx(uint extended,string name,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr DefWindowProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]private static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern IntPtr LoadCursor(IntPtr instance,IntPtr name);
    [DllImport("user32.dll")]private static extern IntPtr SetCursor(IntPtr cursor);
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref uint value,int size);
    [DllImport("user32.dll")]private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll",SetLastError=true)]private static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr screen,ref NativePoint destination,ref NativeSize size,IntPtr source,ref NativePoint sourcePoint,uint key,ref Blend blend,uint flags);
}

internal sealed class WidgetPosition
{
    public int X { get; set; }
    public int Y { get; set; }
}
