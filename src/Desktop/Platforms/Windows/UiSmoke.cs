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
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: native MAUI pages, navigation and render\n"+
                string.Join('\n',Services.AppRuntime.Current.Readings.Select(d=>$"{d.Name}: {d.Percent}% / {d.Transport} / {d.Provider}")));
            Services.AppRuntime.Current.Exit();
        }
        catch(Exception error){File.WriteAllText(Path.Combine(folder,"result.txt"),error.ToString());Environment.Exit(1);}
    }
    private static string CreateFile(string path){File.WriteAllBytes(path,[]);return path;}
}
