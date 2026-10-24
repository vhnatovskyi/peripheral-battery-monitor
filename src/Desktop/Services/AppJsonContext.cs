using System.Text.Json.Serialization;

namespace PeripheralBatteryMonitor.Desktop.Services;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive=true,WriteIndented=true)]
[JsonSerializable(typeof(MonitorSettings))]
[JsonSerializable(typeof(ReleaseInfo[]))]
internal partial class AppJsonContext : JsonSerializerContext { }
