using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal static class Design
{
    public static bool IsDark=>Application.Current?.RequestedTheme!=AppTheme.Light;
    public static Label Label(string text,int size=14,bool bold=false)=>new() { Text=text,FontSize=size,FontAttributes=bold?FontAttributes.Bold:FontAttributes.None };
    public static Label Hint(string text){var label=new Label { Text=text,FontSize=11 };label.SetAppThemeColor(Microsoft.Maui.Controls.Label.TextColorProperty,Color.FromArgb("#52647A"),Color.FromArgb("#95A3B8"));return label;}
    public static Border Panel(View content){var panel=new Border { Content=content,Padding=14,StrokeShape=new RoundRectangle { CornerRadius=12 } };ThemePanel(panel);return panel;}
    public static void ThemePanel(Border panel){panel.SetAppThemeColor(VisualElement.BackgroundColorProperty,Colors.White,Color.FromArgb("#1B2635"));panel.SetAppTheme<Brush>(Border.StrokeProperty,new SolidColorBrush(Color.FromArgb("#D9E2ED")),new SolidColorBrush(Color.FromArgb("#2C3A4D")));}
    public static string T(string uk,string en)=>AppRuntime.Current.Text(uk,en);
    public static VerticalStackLayout Stack(params View[] children)
    {var stack=new VerticalStackLayout { Spacing=10 };foreach(var child in children)stack.Add(child);return stack;}
    public static Button Button(string text,EventHandler clicked)
    {var button=new Button { Text=text };button.Clicked+=clicked;return button;}
}

