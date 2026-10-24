param([Parameter(Mandatory=$true)][string]$InstallRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Load-InstallPaths.ps1')
$install=[PeripheralBatteryMonitor.Installation.InstallPaths]::ValidateUninstall($InstallRoot,$PSCommandPath)
Add-Type -AssemblyName System.Windows.Forms
if ([Windows.Forms.MessageBox]::Show('Uninstall Peripheral Battery Monitor? Your settings will be kept.', 'Peripheral Battery Monitor', 'YesNo', 'Question') -ne 'Yes') { exit }
foreach($eventName in @('Local\PeripheralBatteryMonitor.UI.Exit','Local\PeripheralBatteryMonitor.Exit')){try { $event=[Threading.EventWaitHandle]::OpenExisting($eventName);$event.Set() | Out-Null;$event.Dispose() } catch [Threading.WaitHandleCannotBeOpenedException] { }}
$mutex=New-Object Threading.Mutex($false,'Local\AJ139BatteryIndicator')
try {
    try {$acquired=$mutex.WaitOne(10000)} catch [Threading.AbandonedMutexException] {$acquired=$true}
    if(-not $acquired){throw 'Close the application and retry.'}
    try {
        # Revalidate the absolute registered target immediately before removing it.
        $checked=[PeripheralBatteryMonitor.Installation.InstallPaths]::ValidateUninstall($install,$PSCommandPath)
        if($checked -ne $install){throw 'Installation directory changed.'}
        foreach($process in Get-Process -Name PeripheralBatteryMonitor,PeripheralBatteryMonitor.Desktop,PeripheralBatteryMonitor.Agent -ErrorAction SilentlyContinue){
            if($process.Path -and $process.Path.StartsWith($install+'\',[StringComparison]::OrdinalIgnoreCase) -and -not $process.WaitForExit(5000)){throw 'The application is still closing. Please retry.'}
        }
        Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name PeripheralBatteryMonitor -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'Peripheral Battery Monitor.lnk') -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PeripheralBatteryMonitor' -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $install) { Remove-Item -LiteralPath $install -Recurse -Force }
    }finally{$mutex.ReleaseMutex()}
}finally{$mutex.Dispose()}
