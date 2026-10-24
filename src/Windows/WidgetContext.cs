using System.Text.Json.Serialization;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

internal sealed class WidgetContext(Func<MonitorSettings> settings,Func<bool> dark,Func<IntPtr> owner,string stateRoot)
{
    public MonitorSettings Settings=>settings();
    public bool IsDark=>dark();
    public IntPtr Owner=>owner();
    public string StateRoot=>stateRoot;
    public string Text(string uk,string en)=>Settings.Language=="uk"?uk:en;
}

internal static class NativeIcons
{
    public static string FontFamily=>OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)?"Segoe Fluent Icons":"Segoe MDL2 Assets";
    public static string Glyph(string kind)=>kind switch { "mouse"=>"\uE962","keyboard"=>"\uE765","headset"=>"\uE7F6","controller"=>"\uE7FC","refresh"=>"\uE72C","unplug"=>"\uE8CD",_=>"\uE772" };
}

[JsonSerializable(typeof(WidgetPosition))]
internal partial class WidgetJsonContext:JsonSerializerContext { }
