using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Windowing;
using PeripheralBatteryMonitor.Desktop.Services;
using Native = Microsoft.UI.Xaml;
using Controls = Microsoft.UI.Xaml.Controls;
using Media = Microsoft.UI.Xaml.Media;
using Point = Windows.Graphics.PointInt32;
using Rect = Windows.Graphics.RectInt32;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

// Interaction is explicitly armed from Settings. Switching to ANY other app
// disarms it, including windowed games that fullscreen heuristics cannot detect.
internal sealed class BatteryWidget : IDisposable
{
    internal Native.Window Window { get; } = new();
    internal bool IsMoving { get; private set; }
    internal int DeviceCount { get; private set; }
    internal bool ClickThrough => (GetWindowLong(handle,-20)&0x20)!=0;
    internal byte NativeOpacity { get { GetLayeredWindowAttributes(handle,out _,out byte alpha,out _);return alpha; } }
    internal byte BackgroundOpacity => ((Media.SolidColorBrush)frame.Background).Color.A;
    internal bool HasTransparentBackdrop => Window.SystemBackdrop is TransparentWidgetBackdrop backdrop && backdrop.IsConnected;
    private readonly IntPtr handle,owner;
    private readonly bool persist;
    private readonly Native.DispatcherTimer guard=new() { Interval=TimeSpan.FromMilliseconds(100) };
    private string fingerprint="";
    private bool disposed,positionDirty;
    private DateTime moved;
    private uint dpi;
    private IReadOnlyList<DeviceReading> readings=[];
    private readonly Controls.StackPanel rows=new() { Spacing=8 };
    private readonly Native.Controls.Border frame;

    public BatteryWidget(Native.Window main,bool persistPosition=true)
    {
        persist=persistPosition;owner=WinRT.Interop.WindowNative.GetWindowHandle(main);
        handle=WinRT.Interop.WindowNative.GetWindowHandle(Window);
        Window.Title="Peripheral Battery Monitor — Widget";
        var presenter=OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false,false);
        presenter.IsResizable=false;presenter.IsMaximizable=false;presenter.IsMinimizable=false;presenter.IsAlwaysOnTop=true;
        Window.AppWindow.SetPresenter(presenter);Window.AppWindow.IsShownInSwitchers=false;
        frame=new Native.Controls.Border { Padding=new Native.Thickness(10),BorderThickness=new Native.Thickness(1),CornerRadius=new Native.CornerRadius(10),Child=rows };
        frame.PointerPressed+=(_,e)=>
        {
            if(!CanMove(GetForegroundWindow()==owner || GetForegroundWindow()==handle,IsMoving)) { SetMoving(false);return; }
            if(!e.GetCurrentPoint(frame).Properties.IsLeftButtonPressed)return;
            ReleaseCapture();SendMessage(handle,0xA1,new IntPtr(2),IntPtr.Zero); // Native caption drag, supports multiple displays.
            e.Handled=true;
        };
        Window.Content=new Controls.ScrollViewer { Content=frame,VerticalScrollBarVisibility=Controls.ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=Controls.ScrollBarVisibility.Disabled };
        Window.SystemBackdrop=new TransparentWidgetBackdrop();
        int style=GetWindowLong(handle,-20);
        SetWindowLong(handle,-20,(style|0x80000|0x80|0x08000000)&~0x40000); // Layered, tool window, no activation or taskbar button.
        SetLayeredWindowAttributes(handle,0,255,2);
        SetMoving(false);RestorePosition(main);Update([]);
        Window.AppWindow.Changed+=(_,e)=>{if(e.DidPositionChange){positionDirty=true;moved=DateTime.UtcNow;}};
        Window.AppWindow.Closing+=(_,e)=>{if(!disposed)e.Cancel=true;};
        guard.Tick+=(_,_)=>
        {
            CheckInteractionFocus(GetForegroundWindow()==owner || GetForegroundWindow()==handle);
            if(positionDirty && DateTime.UtcNow-moved>TimeSpan.FromMilliseconds(600))SavePosition();
            if(dpi!=GetDpiForWindow(handle)){fingerprint="";Update(readings);}
        };
        guard.Start();Window.AppWindow.Show(false);
    }

    internal static bool CanMove(bool monitorHasFocus,bool armed)=>monitorHasFocus && armed;
    internal void CheckInteractionFocus(bool monitorHasFocus){if(IsMoving && !CanMove(monitorHasFocus,true))SetMoving(false);}
    public void BeginMove(){if(GetForegroundWindow()==owner)SetMoving(true);}
    internal void SetMoving(bool value)
    {
        IsMoving=value;
        int style=GetWindowLong(handle,-20);
        SetWindowLong(handle,-20,value?style&~0x20:style|0x20);
        // Flush cached extended styles without activating the overlay.
        SetWindowPos(handle,IntPtr.Zero,0,0,0,0,0x37);
        fingerprint="";Update(readings);
    }
    public void Update(IReadOnlyList<DeviceReading> value)
    {
        if(disposed)return;
        readings=value;
        bool dark=Views.Design.IsDark;
        int opacity=Math.Clamp(AppRuntime.Current.Settings.Value.WidgetOpacity,20,100);
        string next=$"{dark}|{IsMoving}|{opacity}|{AppRuntime.Current.Settings.Value.Language}|"+string.Join('|',value.Select(d=>$"{d.Id}:{d.Name}:{d.Transport}:{d.Percent}:{d.Accent}"));
        if(fingerprint==next)return;fingerprint=next;DeviceCount=value.Count;
        var foreground=Brush(dark?"#F3F6FA":"#182435");
        var secondary=Brush(dark?"#A5B4C8":"#52647A");
        frame.Background=Brush(dark?"#172232":"#F5F8FC",alpha:(byte)Math.Round(opacity*2.55));
        frame.BorderBrush=Brush(IsMoving?"#399BDE":dark?"#34465D":"#CBD8E8",alpha:IsMoving?(byte)255:(byte)Math.Round(opacity*2.55));
        Controls.ToolTipService.SetToolTip(frame,IsMoving?AppRuntime.Current.Text("Перетягніть віджет","Drag to move widget"):null);
        rows.Children.Clear();
        foreach(var device in value)
        {
            var grid=new Controls.Grid { ColumnSpacing=8 };
            grid.ColumnDefinitions.Add(new() { Width=new Native.GridLength(1,Native.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new() { Width=Native.GridLength.Auto });
            var name=new Controls.TextBlock { Text=device.Name,FontSize=12,Foreground=foreground,TextTrimming=Native.TextTrimming.CharacterEllipsis,VerticalAlignment=Native.VerticalAlignment.Center };
            var percent=new Controls.TextBlock { Text=device.Percent is int p?$"{p}%":"—",FontSize=18,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Foreground=Brush(device.Accent,dark?1:0.55) };
            Controls.Grid.SetColumn(percent,1);grid.Children.Add(name);grid.Children.Add(percent);
            var bar=new Controls.ProgressBar { Minimum=0,Maximum=100,Value=device.Percent??0,Height=3,Foreground=Brush(device.Accent),Background=Brush(dark?"#34465D":"#DCE5F0"),Margin=new Native.Thickness(0,3,0,0) };
            var row=new Controls.StackPanel();row.Children.Add(grid);row.Children.Add(bar);rows.Children.Add(row);
        }
        if(value.Count==0)rows.Children.Add(new Controls.TextBlock { Text=AppRuntime.Current.Text("Немає підключених пристроїв","No connected devices"),FontSize=12,Foreground=secondary });
        dpi=GetDpiForWindow(handle);double scale=dpi/96d;
        var area=DisplayArea.GetFromWindowId(Window.AppWindow.Id,DisplayAreaFallback.Nearest).WorkArea;
        int height=Math.Min(area.Height-16,(int)Math.Ceiling((22+Math.Max(1,value.Count)*40)*scale));
        // A scroll container keeps large device collections reachable within the display.
        Window.AppWindow.Resize(new((int)Math.Ceiling(280*scale),Math.Max(55,height)));
        KeepOnDisplay(area);
    }
    private void RestorePosition(Native.Window main)
    {
        var area=DisplayArea.GetFromWindowId(main.AppWindow.Id,DisplayAreaFallback.Nearest).WorkArea;
        var position=new WidgetPosition { X=area.X+area.Width-300,Y=area.Y+24 };
        if(persist)try { position=JsonSerializer.Deserialize(File.ReadAllText(PositionPath),AppJsonContext.Default.WidgetPosition)??position; }
        catch(Exception error) when(error is IOException or JsonException or UnauthorizedAccessException) { }
        area=DisplayArea.GetFromRect(new Rect(position.X,position.Y,280,120),DisplayAreaFallback.Nearest).WorkArea;
        Window.AppWindow.Move(new Point(position.X,position.Y));KeepOnDisplay(area);
    }
    private void KeepOnDisplay(Rect area)
    {
        var p=Window.AppWindow.Position;var size=Window.AppWindow.Size;
        var clamped=new Point(Math.Clamp(p.X,area.X,Math.Max(area.X,area.X+area.Width-size.Width)),Math.Clamp(p.Y,area.Y,Math.Max(area.Y,area.Y+area.Height-size.Height)));
        if(p.X!=clamped.X || p.Y!=clamped.Y)Window.AppWindow.Move(clamped);
    }
    private static string PositionPath=>Path.Combine(SettingsStore.StateRoot,"widget-position.json");
    private void SavePosition()
    {
        positionDirty=false;if(!persist)return;
        try
        {
            var p=Window.AppWindow.Position;Directory.CreateDirectory(SettingsStore.StateRoot);
            File.WriteAllText(PositionPath+".pending",JsonSerializer.Serialize(new WidgetPosition { X=p.X,Y=p.Y },AppJsonContext.Default.WidgetPosition));
            File.Move(PositionPath+".pending",PositionPath,true);
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException) { }
    }
    private static Media.SolidColorBrush Brush(string hex,double brightness=1,byte alpha=255)
    {var c=Microsoft.Maui.Graphics.Color.FromArgb(hex);return new(global::Windows.UI.Color.FromArgb(alpha,(byte)(c.Red*255*brightness),(byte)(c.Green*255*brightness),(byte)(c.Blue*255*brightness)));}
    public void Dispose(){if(disposed)return;SavePosition();disposed=true;guard.Stop();Window.Close();}
    [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]private static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]private static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll")]private static extern bool SetLayeredWindowAttributes(IntPtr hwnd,uint key,byte alpha,uint flags);
    [DllImport("user32.dll")]private static extern bool GetLayeredWindowAttributes(IntPtr hwnd,out uint key,out byte alpha,out uint flags);
    [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr hwnd);
}

// A transparent composition backdrop lets the panel use per-pixel alpha while
// text and progress indicators retain full opacity. No global window fade.
internal sealed class TransparentWidgetBackdrop : Media.SystemBackdrop
{
    private global::Windows.UI.Composition.Compositor? compositor;
    private global::Windows.UI.Composition.CompositionColorBrush? brush;
    internal bool IsConnected=>brush!=null;
    protected override void OnTargetConnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop target,Native.XamlRoot root)
    {
        base.OnTargetConnected(target,root);
        compositor=new global::Windows.UI.Composition.Compositor();
        brush=compositor.CreateColorBrush(global::Windows.UI.Color.FromArgb(0,0,0,0));
        target.SystemBackdrop=brush;
    }
    protected override void OnTargetDisconnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop target)
    {
        target.SystemBackdrop=null;brush?.Dispose();brush=null;compositor?.Dispose();compositor=null;base.OnTargetDisconnected(target);
    }
}

internal sealed class WidgetPosition
{
    public int X { get; set; }
    public int Y { get; set; }
}
