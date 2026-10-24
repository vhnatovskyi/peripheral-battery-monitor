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
            string customRoot=Path.Combine(Path.GetTempPath(),"Custom apps","Заряд пристроїв");
            if(Services.SettingsStore.ResolveInstalledRoot(Path.Combine(customRoot,"versions","1.2.3"))!=customRoot || Services.SettingsStore.ResolveInstalledRoot(customRoot)!=null)
                throw new InvalidOperationException("Custom installation root detection failed");
            var installerStart=Services.ReleaseService.CreateInstallerStart(Path.Combine(folder,"Install.ps1"),folder,customRoot);
            int rootArgument=installerStart.ArgumentList.IndexOf("-InstallRoot");
            if(rootArgument<0 || installerStart.ArgumentList[rootArgument+1]!=customRoot)
                throw new InvalidOperationException("Updater must retain the selected installation folder as one argument");
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
                await WidgetSmoke.RunAsync(window,folder,Services.AppRuntime.Current.Readings);
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
