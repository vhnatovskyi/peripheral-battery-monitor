using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class AboutPage : ContentPage
{
    public AboutPage(){AppRuntime.Current.SettingsChanged+=Build;Build();}
    private void Build()
    {
        var image=new Image { Source="brand.png",HeightRequest=72,WidthRequest=72,HorizontalOptions=LayoutOptions.Start };
        var header=new Grid { ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star)],ColumnSpacing=16 };
        header.Add(image);
        var heading=Design.Stack(Design.Label(Design.T("Заряд пристроїв під рукою","Device battery at a glance"),22,true),Design.Hint("v"+AppRuntime.Current.Settings.Version+" · .NET MAUI"));
        heading.Spacing=5;heading.VerticalOptions=LayoutOptions.Center;header.Add(heading,1);
        var stack=Design.Stack(header,
            Design.Panel(Design.Stack(Design.Hint(Design.T("Розробник","Developer")),Design.Label("Vladyslav Hnatovskyi",20,true),
                Link("GitHub", "https://github.com/vhnatovskyi/peripheral-battery-monitor"),Link("LinkedIn","https://www.linkedin.com/in/vladyslav-hnatovskyi/"),Link("vhnatovskyi@hotmail.com","mailto:vhnatovskyi@hotmail.com"))),
            Design.Hint(Design.T("Заряд залежить від даних пристрою. Для власних протоколів потрібна окрема підтримка.","Battery readings depend on the device. Proprietary protocols require a device provider.")));
        stack.Padding=16;Content=new ScrollView { Content=stack };
    }
    private static Button Link(string title,string uri)=>Design.Button(title,async(_,_)=>await Launcher.Default.OpenAsync(uri));
}
