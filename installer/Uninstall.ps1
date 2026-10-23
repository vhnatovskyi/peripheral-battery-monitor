param([Parameter(Mandatory=$true)][string]$InstallRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
if ([Windows.Forms.MessageBox]::Show('Uninstall Peripheral Battery Monitor? Your settings will be kept.', 'Peripheral Battery Monitor', 'YesNo', 'Question') -ne 'Yes') { exit }
$install=[IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$expected=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\PeripheralBatteryMonitor')).TrimEnd('\')
if ($install -ne $expected) { throw 'Unexpected installation directory.' }
try { $event=[Threading.EventWaitHandle]::OpenExisting('Local\PeripheralBatteryMonitor.Exit');$event.Set() | Out-Null;$event.Dispose() } catch [Threading.WaitHandleCannotBeOpenedException] { }
$mutex=New-Object Threading.Mutex($false,'Local\AJ139BatteryIndicator')
try {
    try {$acquired=$mutex.WaitOne(10000)} catch [Threading.AbandonedMutexException] {$acquired=$true}
    if(-not $acquired){throw 'Close the application and retry.'}
    try {
        Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name PeripheralBatteryMonitor -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'Peripheral Battery Monitor.lnk') -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PeripheralBatteryMonitor' -ErrorAction SilentlyContinue
        if ($install -eq $expected -and (Test-Path -LiteralPath $install)) { Remove-Item -LiteralPath $install -Recurse -Force }
    }finally{$mutex.ReleaseMutex()}
}finally{$mutex.Dispose()}
