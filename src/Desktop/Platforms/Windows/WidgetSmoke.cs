using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.UI.Windowing;
using PeripheralBatteryMonitor.Desktop.Services;
using Color=System.Drawing.Color;
using ImageFormat=System.Drawing.Imaging.ImageFormat;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

// Inspect actual desktop composition over an app-owned two-color test window.
// RenderTargetBitmap alone cannot catch an opaque HWND behind transparent XAML.
internal static class WidgetSmoke
{
    internal static async Task RunAsync(Microsoft.UI.Xaml.Window main,string folder,IReadOnlyList<DeviceReading> readings)
    {
        int originalOpacity=AppRuntime.Current.Settings.Value.WidgetOpacity;
        var preferences=AppRuntime.Current.Settings.Value;
        var originalHidden=preferences.WidgetHiddenDevices;
        var originalMode=preferences.WidgetMode;
        var originalOrientation=preferences.WidgetOrientation;
        preferences.WidgetOrientation="Horizontal";
        preferences.WidgetMode="Standard";
        preferences.WidgetHiddenDevices=[];
        var background=new Microsoft.UI.Xaml.Window { Title="PBM widget transparency test" };
        var grid=new Microsoft.UI.Xaml.Controls.Grid();
        grid.ColumnDefinitions.Add(new());grid.ColumnDefinitions.Add(new());
        var left=Color.FromArgb(200,216,232);var right=Color.FromArgb(230,178,196);
        grid.Children.Add(new Microsoft.UI.Xaml.Controls.Border { Background=Brush(left) });
        var rightPanel=new Microsoft.UI.Xaml.Controls.Border { Background=Brush(right) };
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(rightPanel,1);grid.Children.Add(rightPanel);background.Content=grid;
        var presenter=OverlappedPresenter.Create();presenter.SetBorderAndTitleBar(false,false);presenter.IsAlwaysOnTop=true;background.AppWindow.SetPresenter(presenter);
        try
        {
            using var widget=new BatteryWidget(main,false);widget.Update(readings);
            if(!widget.IsAlwaysOnTop || widget.HasBorder || !widget.ClickThrough || widget.DeviceCount!=3)throw new InvalidOperationException("Widget must be borderless, passive and topmost");
            var draft=preferences.Copy();draft.WidgetHiddenDevices=[readings[0].Id];
            if(!preferences.ShowInWidget(readings[0].Id))throw new InvalidOperationException("Draft changed saved preferences");
            preferences.WidgetHiddenDevices=draft.WidgetHiddenDevices;widget.Update(readings);
            draft.TrayHiddenDevices=[readings[1].Id];
            if(!preferences.ShowInTray(readings[1].Id) || TrayHost.VisibleDevices(readings,draft).Any(d=>d.Id==readings[1].Id) || !draft.ShowInTray(readings[0].Id))
                throw new InvalidOperationException("Tray and widget visibility must be independent");
            var json=System.Text.Json.JsonSerializer.Serialize(draft,AppJsonContext.Default.MonitorSettings);
            var restored=System.Text.Json.JsonSerializer.Deserialize(json,AppJsonContext.Default.MonitorSettings)!;
            if(restored.ShowInTray(readings[1].Id) || restored.ShowInWidget(readings[0].Id))throw new InvalidOperationException("Visibility choices did not survive serialization");
            if(widget.DeviceCount!=2 || readings.Count!=3)throw new InvalidOperationException("Widget hiding must affect only the widget");
            preferences.WidgetHiddenDevices=readings.Select(d=>d.Id).ToArray();widget.Update(readings);
            if(widget.DeviceCount!=0)throw new InvalidOperationException("Hiding all widget devices failed");
            preferences.WidgetHiddenDevices=[];widget.Update(readings);
            if(widget.DeviceCount!=3)throw new InvalidOperationException("Restoring widget devices failed");
            int standardHeight=widget.Bounds.Height;
            preferences.WidgetMode="Minimized";widget.Update(readings);
            if(widget.DeviceCount!=3 || widget.Bounds.Height>=standardHeight)throw new InvalidOperationException("Minimized mode did not compact the device rows");
            using(var minimized=widget.RenderPreview())minimized.Save(Path.Combine(folder,"minimized-widget.png"),ImageFormat.Png);
            var horizontalSize=widget.Bounds.Size;
            preferences.WidgetOrientation="Vertical";widget.Update(readings);
            if(widget.Bounds.Width>=horizontalSize.Width || widget.Bounds.Height<=horizontalSize.Height)throw new InvalidOperationException("Vertical Minimized layout did not stack devices");
            using(var vertical=widget.RenderPreview())vertical.Save(Path.Combine(folder,"minimized-vertical-widget.png"),ImageFormat.Png);
            widget.Update(readings.Take(1).ToArray());
            if(widget.DeviceCount!=1 || widget.Bounds.Width>=200)throw new InvalidOperationException("Minimized widget did not resize for one device");
            preferences.WidgetMode="Standard";preferences.WidgetOrientation="Horizontal";widget.Update(readings);
            widget.SetMoving(true);
            if(widget.ClickThrough || !widget.IsMoving)throw new InvalidOperationException("Positioning must enable dragging");
            widget.CheckInteractionFocus(false);
            if(!widget.ClickThrough || widget.IsMoving)throw new InvalidOperationException("Switching away must restore click-through");
            widget.CheckInteractionFocus(true);
            if(!widget.ClickThrough)throw new InvalidOperationException("Returning must not silently unlock dragging");
            widget.Update(readings.Take(1).ToArray());
            if(widget.DeviceCount!=1)throw new InvalidOperationException("Disconnected devices remain in widget");
            widget.Update(readings);
            var area=DisplayArea.GetFromWindowId(main.AppWindow.Id,DisplayAreaFallback.Nearest).WorkArea;
            var size=widget.Bounds.Size;
            background.AppWindow.MoveAndResize(new(area.X+40,area.Y+40,size.Width+80,size.Height+80));
            background.AppWindow.Show(false);await Task.Delay(500);
            widget.MoveTo(area.X+80,area.Y+80);widget.Raise();
            foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark})
            {
                Application.Current!.UserAppTheme=theme;
                foreach(string mode in new[]{"Standard","Minimized","Vertical"})
                {
                preferences.WidgetMode=mode=="Standard"?"Standard":"Minimized";
                preferences.WidgetOrientation=mode=="Vertical"?"Vertical":"Horizontal";
                foreach(int opacity in new[]{0,20,50,100})
                {
                    AppRuntime.Current.Settings.Value.WidgetOpacity=opacity;widget.Update(readings);
                    var current=widget.Bounds.Size;background.AppWindow.MoveAndResize(new(area.X+40,area.Y+40,current.Width+80,current.Height+80));
                    widget.MoveTo(area.X+80,area.Y+80);widget.Raise();await Task.Delay(250);
                    using var preview=widget.RenderPreview();
                    if(preview.GetPixel(0,0).A!=0)throw new InvalidOperationException("Widget corners must be transparent without a frame");
                    using var capture=new Bitmap(widget.Bounds.Width,widget.Bounds.Height,PixelFormat.Format32bppArgb);
                    using(var graphics=Graphics.FromImage(capture))graphics.CopyFromScreen(widget.Bounds.Location,System.Drawing.Point.Empty,capture.Size);
                    var tint=ColorTranslator.FromHtml(theme==AppTheme.Dark?"#172232":"#F5F8FC");
                    AssertBlend(capture.GetPixel(5,capture.Height/2),left,tint,opacity);
                    AssertBlend(capture.GetPixel(capture.Width-6,capture.Height/2),right,tint,opacity);
                    if(opacity==50)
                    {
                        string prefix=theme.ToString().ToLowerInvariant()+(mode=="Standard"?"":mode=="Vertical"?"-minimized-vertical":"-minimized");
                        preview.Save(Path.Combine(folder,prefix+"-widget.png"),ImageFormat.Png);
                        capture.Save(Path.Combine(folder,prefix+"-widget-desktop.png"),ImageFormat.Png);
                        int opaque=0;
                        for(int y=0;y<preview.Height;y++)for(int x=0;x<preview.Width;x++)if(preview.GetPixel(x,y).A==255)opaque++;
                        if(opaque<100)throw new InvalidOperationException("Widget text and bars must stay opaque");
                    }
                }
                }
            }
            File.WriteAllText(Path.Combine(folder,"widget-result.txt"),"PASS: real desktop composition at 0%, 20%, 50%, 100% background opacity in both themes; no window border; opaque content; focus guard; independent visibility");
        }
        finally { background.Close();preferences.WidgetOpacity=originalOpacity;preferences.WidgetHiddenDevices=originalHidden;preferences.WidgetMode=originalMode;preferences.WidgetOrientation=originalOrientation; }
    }
    private static void AssertBlend(Color actual,Color backdrop,Color tint,int opacity)
    {
        int alpha=(int)Math.Round(opacity*2.55);
        int Blend(int foreground,int behind)=>(foreground*alpha+behind*(255-alpha)+127)/255;
        if(Math.Abs(actual.R-Blend(tint.R,backdrop.R))>8 || Math.Abs(actual.G-Blend(tint.G,backdrop.G))>8 || Math.Abs(actual.B-Blend(tint.B,backdrop.B))>8)
            throw new InvalidOperationException($"Actual desktop transparency failed: {actual}; expected tint {tint} at {opacity}% over {backdrop}");
    }
    private static Microsoft.UI.Xaml.Media.SolidColorBrush Brush(Color color)=>new(global::Windows.UI.Color.FromArgb(255,color.R,color.G,color.B));
}
