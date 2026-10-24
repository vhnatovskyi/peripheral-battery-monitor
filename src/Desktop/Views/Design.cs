using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal static class Design
{
    public static Color Muted=Color.FromArgb("#95A3B8");
    public static Label Label(string text,int size=14,bool bold=false)=>new() { Text=text,FontSize=size,FontAttributes=bold?FontAttributes.Bold:FontAttributes.None };
    public static Label Hint(string text)=>new() { Text=text,TextColor=Muted,FontSize=12 };
    public static Border Panel(View content)=>new() { Content=content,Padding=20,BackgroundColor=Color.FromArgb("#1B2635"),Stroke=Color.FromArgb("#2C3A4D"),StrokeShape=new RoundRectangle { CornerRadius=16 } };
    public static string T(string uk,string en)=>AppRuntime.Current.Text(uk,en);
    public static VerticalStackLayout Stack(params View[] children)
    {var stack=new VerticalStackLayout { Spacing=16 };foreach(var child in children)stack.Add(child);return stack;}
    public static Button Button(string text,EventHandler clicked)
    {var button=new Button { Text=text };button.Clicked+=clicked;return button;}
}

