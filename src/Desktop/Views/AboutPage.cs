using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class AboutPage : ContentPage
{
    public AboutPage(){AppRuntime.Current.SettingsChanged+=Build;Build();}
    private void Build()
    {
        var image=new Image { Source="brand.png",HeightRequest=72,WidthRequest=72,HorizontalOptions=LayoutOptions.Start };
        var stack=Design.Stack(image,Design.Label(Design.T("Заряд пристроїв під рукою","Device battery at a glance"),24,true),Design.Hint("v"+AppRuntime.Current.Settings.Version+" · .NET MAUI"),
            Design.Panel(Design.Stack(Design.Hint(Design.T("Розробник","Developer")),Design.Label("Vladyslav Hnatovskyi",20,true),
                Link("GitHub", "https://github.com/vhnatovskyi/peripheral-battery-monitor"),Link("LinkedIn","https://www.linkedin.com/in/vladyslav-hnatovskyi/"),Link("vhnatovskyi@hotmail.com","mailto:vhnatovskyi@hotmail.com"))),
            Design.Hint(Design.T("Заряд залежить від даних пристрою. Для власних протоколів потрібна окрема підтримка.","Battery readings depend on the device. Proprietary protocols require a device provider.")));
        stack.Padding=24;Content=new ScrollView { Content=stack };
    }
    private static Button Link(string title,string uri)=>Design.Button(title,async(_,_)=>await Launcher.Default.OpenAsync(uri));
}
