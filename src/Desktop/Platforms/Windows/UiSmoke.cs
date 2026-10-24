using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

internal static class UiSmoke
{
    public static async Task RunAsync(Microsoft.UI.Xaml.Window window,string folder)
    {
        folder=Path.GetFullPath(folder);
        Directory.CreateDirectory(folder);
        try
        {
            await Task.Delay(1400);
            var shell=(AppShell)Shell.Current;
            if(window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter && presenter.IsMaximizable)
                throw new InvalidOperationException("Window maximization should be disabled");
            if(!window.Title.Contains(Services.AppRuntime.Current.Settings.Version))throw new InvalidOperationException("Version missing from title");
            var testCard=new Views.DeviceCardView();
            var sample=new Services.DeviceReading("test","Mouse","Bluetooth",92,"#55DEAD","Demo",DateTime.UtcNow,null);
            testCard.Update(sample);
            var image=(Image)typeof(Views.DeviceCardView).GetField("icon",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(testCard)!;
            var source=image.Source;
            for(int value=91;value>=80;value--)testCard.Update(sample with { Percent=value });
            if(!ReferenceEquals(source,image.Source))throw new InvalidOperationException("State update restarted the device icon load");
            foreach(string route in new[]{"devices","settings","about","releases"})
            {
                shell.Navigate(route);await Task.Delay(route=="releases"?1800:600);
                if(route=="devices")
                {
                    var toolbar=(Grid)Shell.GetTitleView(Shell.Current.CurrentPage);
                    var refresh=toolbar.Children.OfType<ImageButton>().Single(b=>b.AutomationId=="RefreshDevices");
                    if(string.IsNullOrWhiteSpace(ToolTipProperties.GetText(refresh)?.ToString()))throw new InvalidOperationException("Refresh tooltip missing");
                    if(refresh.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject native && Microsoft.UI.Xaml.Controls.ToolTipService.GetToolTip(native)==null)
                        throw new InvalidOperationException("Native refresh tooltip missing");
                }
                if(Shell.Current.CurrentPage.Handler==null)throw new InvalidOperationException("Missing native page handler: "+route);
                var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync((Microsoft.UI.Xaml.UIElement)window.Content);
                if(bitmap.PixelWidth<300||bitmap.PixelHeight<200)throw new InvalidOperationException("Invalid native layout size");
                var buffer=await bitmap.GetPixelsAsync();
                var file=await StorageFile.GetFileFromPathAsync(CreateFile(Path.Combine(folder,route+".png")));
                using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,buffer.ToArray());
                await encoder.FlushAsync();
            }
            if(Environment.GetCommandLineArgs().Contains("--ui-smoke"))
            foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark})
            {
                Application.Current!.UserAppTheme=theme;shell.Navigate("devices");await Task.Delay(500);
                var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync((Microsoft.UI.Xaml.UIElement)window.Content);
                var buffer=await bitmap.GetPixelsAsync();
                var file=await StorageFile.GetFileFromPathAsync(CreateFile(Path.Combine(folder,theme.ToString().ToLowerInvariant()+"-devices.png")));
                using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,buffer.ToArray());
                await encoder.FlushAsync();
            }
            if(Environment.GetCommandLineArgs().Contains("--ui-smoke"))
            {
                using(var widget=new BatteryWidget(window,false))
                {
                    widget.Update(Services.AppRuntime.Current.Readings);
                    if(widget.Window.AppWindow.Presenter is not Microsoft.UI.Windowing.OverlappedPresenter overlay || !overlay.IsAlwaysOnTop || overlay.IsResizable)
                        throw new InvalidOperationException("Widget must remain on top without resize borders");
                    if(widget.Window.AppWindow.IsShownInSwitchers || !widget.ClickThrough || widget.DeviceCount!=3)
                        throw new InvalidOperationException("Widget must be passive and display connected devices");
                    widget.SetMoving(true);
                    if(widget.ClickThrough || !widget.IsMoving)throw new InvalidOperationException("Explicit widget positioning did not unlock dragging");
                    widget.CheckInteractionFocus(false); // Windowed games and other apps must disarm dragging.
                    if(!widget.ClickThrough || widget.IsMoving)throw new InvalidOperationException("Switching away must restore click-through");
                    widget.CheckInteractionFocus(true);
                    if(!widget.ClickThrough)throw new InvalidOperationException("Returning to the app must not silently unlock the widget");
                    widget.Update(Services.AppRuntime.Current.Readings.Take(1).ToArray());
                    if(widget.DeviceCount!=1)throw new InvalidOperationException("Disconnected devices remain in the widget");
                    widget.Update(Services.AppRuntime.Current.Readings);
                    int originalOpacity=Services.AppRuntime.Current.Settings.Value.WidgetOpacity;
                    Services.AppRuntime.Current.Settings.Value.WidgetOpacity=50;widget.Update(Services.AppRuntime.Current.Readings);
                    if(widget.NativeOpacity!=255 || widget.BackgroundOpacity is <127 or >128 || !widget.HasTransparentBackdrop)
                        throw new InvalidOperationException("Only the background should fade over a transparent composition backdrop");
                    Services.AppRuntime.Current.Settings.Value.WidgetOpacity=originalOpacity;widget.Update(Services.AppRuntime.Current.Readings);
                    var settings=new Services.MonitorSettings { WidgetEnabled=true,WidgetOpacity=50 };
                    var json=System.Text.Json.JsonSerializer.Serialize(settings,Services.AppJsonContext.Default.MonitorSettings);
                    var restored=System.Text.Json.JsonSerializer.Deserialize(json,Services.AppJsonContext.Default.MonitorSettings);
                    if(restored?.WidgetEnabled!=true || restored.WidgetOpacity!=50)
                        throw new InvalidOperationException("Widget setting did not round-trip");
                    foreach(var theme in new[]{AppTheme.Light,AppTheme.Dark})
                    {
                        Services.AppRuntime.Current.Settings.Value.WidgetOpacity=50;
                        Application.Current!.UserAppTheme=theme;widget.Update(Services.AppRuntime.Current.Readings);await Task.Delay(300);
                        var widgetbitmap=new RenderTargetBitmap();await widgetbitmap.RenderAsync((Microsoft.UI.Xaml.UIElement)widget.Window.Content);
                        if(widgetbitmap.PixelWidth<250 || widgetbitmap.PixelHeight<100)throw new InvalidOperationException("Widget layout failed");
                        var widgetbuffer=await widgetbitmap.GetPixelsAsync();
                        byte[] pixels=widgetbuffer.ToArray();
                        int translucent=0,opaque=0;
                        for(int i=3;i<pixels.Length;i+=4){if(pixels[i] is >100 and <180)translucent++;if(pixels[i]==255)opaque++;}
                        if(translucent<1000 || opaque<100)throw new InvalidOperationException("Widget rendering must retain opaque content over translucent panel pixels");
                        var widgetfile=await StorageFile.GetFileFromPathAsync(CreateFile(Path.Combine(folder,theme.ToString().ToLowerInvariant()+"-widget.png")));
                        using var widgetstream=await widgetfile.OpenAsync(FileAccessMode.ReadWrite);
                        var widgetencoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,widgetstream);
                        widgetencoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)widgetbitmap.PixelWidth,(uint)widgetbitmap.PixelHeight,96,96,widgetbuffer.ToArray());
                        await widgetencoder.FlushAsync();
                    }
                    Services.AppRuntime.Current.Settings.Value.WidgetOpacity=originalOpacity;
                }
                shell.FlyoutIsPresented=true;await Task.Delay(400);
                var bitmap=new RenderTargetBitmap();await bitmap.RenderAsync((Microsoft.UI.Xaml.UIElement)window.Content);
                var buffer=await bitmap.GetPixelsAsync();
                var file=await StorageFile.GetFileFromPathAsync(CreateFile(Path.Combine(folder,"navigation.png")));
                using var stream=await file.OpenAsync(FileAccessMode.ReadWrite);
                var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,buffer.ToArray());
                await encoder.FlushAsync();shell.FlyoutIsPresented=false;
            }
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: native MAUI pages, navigation and render\n"+
                string.Join('\n',Services.AppRuntime.Current.Readings.Select(d=>$"{d.Name}: {d.Percent}% / {d.Transport} / {d.Provider}")));
            Services.AppRuntime.Current.Exit();
        }
        catch(Exception error){File.WriteAllText(Path.Combine(folder,"result.txt"),error.ToString());Environment.Exit(1);}
    }
    private static string CreateFile(string path){File.WriteAllBytes(path,[]);return path;}
}
