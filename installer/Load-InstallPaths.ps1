if (-not ('PeripheralBatteryMonitor.Installation.InstallPaths' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'InstallPaths.cs') -ReferencedAssemblies @('System.dll','System.Core.dll','System.Xml.dll','System.Runtime.Serialization.dll')
}
