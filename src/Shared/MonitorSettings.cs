namespace PeripheralBatteryMonitor.Desktop.Services;

public sealed class MonitorSettings
{
    public string Language { get; set; } = "uk";
    public string Theme { get; set; } = "System";
    public string TrayMode { get; set; } = "Combined";
    public string TrayStyle { get; set; } = "Battery";
    public bool AutoStart { get; set; }
    public bool LowBatteryAlerts { get; set; } = true;
    public int LowBatteryThreshold { get; set; } = 20;
    public bool CheckForUpdates { get; set; } = true;
    public bool WidgetEnabled { get; set; }
    public string WidgetMode { get; set; } = "Standard";
    public string WidgetOrientation { get; set; } = "Horizontal";
    public int WidgetOpacity { get; set; } = 100;
    public string[] WidgetHiddenDevices { get; set; } = [];
    public string[] TrayHiddenDevices { get; set; } = [];
    public bool ShowInWidget(string deviceId) => !WidgetHiddenDevices.Contains(deviceId,StringComparer.OrdinalIgnoreCase);
    public bool ShowInTray(string deviceId) => !TrayHiddenDevices.Contains(deviceId,StringComparer.OrdinalIgnoreCase);
    public MonitorSettings Copy()
    { var copy=(MonitorSettings)MemberwiseClone();copy.WidgetHiddenDevices=WidgetHiddenDevices?.Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()??[];copy.TrayHiddenDevices=TrayHiddenDevices?.Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()??[];return copy; }
}
