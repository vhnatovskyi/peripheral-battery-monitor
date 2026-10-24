using System.Diagnostics;
using System.Text.Json;
using Aj139Battery;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows discovery benchmark");
for (int i=0;i<2;i++) WindowsDeviceDiscovery.Scan(true);
const int scans=8;
long before=GC.GetAllocatedBytesForCurrentThread();
var watch=Stopwatch.StartNew();
int devices=0;
for (int i=0;i<scans;i++) devices=WindowsDeviceDiscovery.Scan(true).Length;
Console.WriteLine(JsonSerializer.Serialize(new {
    Scans=scans, Devices=devices,
    AllocatedBytesPerScan=(GC.GetAllocatedBytesForCurrentThread()-before)/scans,
    ElapsedMilliseconds=watch.ElapsedMilliseconds
}));
