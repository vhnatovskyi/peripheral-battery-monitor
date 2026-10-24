namespace PeripheralBatteryMonitor.Desktop.Services;

public sealed record DeviceReading(string Id,string Name,string Transport,int? Percent,string Accent,string Provider,DateTime UpdatedUtc,string? Address)
{
    public bool CanDisconnect => Transport=="Bluetooth" && !string.IsNullOrWhiteSpace(Address);
    public string Kind => Name.Contains("head",StringComparison.OrdinalIgnoreCase) || Name.Contains("ear",StringComparison.OrdinalIgnoreCase) ? "headset" :
        Name.Contains("keyboard",StringComparison.OrdinalIgnoreCase) ? "keyboard" : Name.Contains("controller",StringComparison.OrdinalIgnoreCase) ? "controller" :
        Name.Contains("mouse",StringComparison.OrdinalIgnoreCase) || Name.Contains("aj139",StringComparison.OrdinalIgnoreCase) ? "mouse" : "device";
}
