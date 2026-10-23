[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PackageRoot,
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA 'Programs\PeripheralBatteryMonitor'),
    [switch]$Interactive,
    [switch]$NoLaunch,
    [switch]$NoIntegration
)
$ErrorActionPreference = 'Stop'
$package = (Resolve-Path -LiteralPath $PackageRoot).Path
$install = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
if ((Split-Path $install -Leaf) -ne 'PeripheralBatteryMonitor') { throw 'Installation folder must be named PeripheralBatteryMonitor.' }
$metadata = Get-Content -Raw -LiteralPath (Join-Path $package 'version.json') | ConvertFrom-Json
$version = [string]$metadata.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
foreach ($required in @('PeripheralBatteryMonitor.exe','src\App\Tray.ps1','installer\Uninstall.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $required) -PathType Leaf)) { throw "Package missing $required" }
}
New-Item -ItemType Directory -Path (Join-Path $install 'versions') -Force | Out-Null
$destination = [IO.Path]::GetFullPath((Join-Path $install "versions\$version"))
if (-not $destination.StartsWith($install + '\versions\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid destination.' }
if (-not (Test-Path -LiteralPath $destination)) {
    $staging = Join-Path $install ('versions\staging-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    Copy-Item -Path (Join-Path $package '*') -Destination $staging -Recurse -Force
    if (-not ([IO.Path]::GetFullPath($staging)).StartsWith($install + '\versions\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging path.' }
    Move-Item -LiteralPath $staging -Destination $destination
}
# Ask the existing application to close cleanly; never kill unrelated PowerShell processes.
$defaultInstall=[IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\PeripheralBatteryMonitor')).TrimEnd('\')
$isolatedTest=$NoIntegration -and $NoLaunch -and ($install -ne $defaultInstall)
if(-not $isolatedTest){try { $exitEvent=[Threading.EventWaitHandle]::OpenExisting('Local\PeripheralBatteryMonitor.Exit'); $exitEvent.Set() | Out-Null; $exitEvent.Dispose() } catch [Threading.WaitHandleCannotBeOpenedException] { }}
$mutexName=if($isolatedTest){'Local\PeripheralBatteryMonitor.Test.'+[Guid]::NewGuid().ToString('N')}else{'Local\AJ139BatteryIndicator'}
$instanceMutex = New-Object Threading.Mutex($false, $mutexName)
try {
    try { $acquired = $instanceMutex.WaitOne(10000) } catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (-not $acquired) { throw 'Please close Peripheral Battery Monitor and run the installer again.' }
    try {
        $launcher = Join-Path $install 'PeripheralBatteryMonitor.exe'
        if (-not (Test-Path -LiteralPath $launcher)) { Copy-Item -LiteralPath (Join-Path $destination 'PeripheralBatteryMonitor.exe') -Destination $launcher }
        $pointer = Join-Path $install 'current.txt'
        $pending = Join-Path $install 'current.pending'
        [IO.File]::WriteAllText($pending, $version)
        if (Test-Path -LiteralPath $pointer) { [IO.File]::Replace($pending, $pointer, (Join-Path $install 'previous.txt')) } else { [IO.File]::Move($pending, $pointer) }
    } finally { $instanceMutex.ReleaseMutex() }
} finally { $instanceMutex.Dispose() }
if (-not $NoIntegration) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Peripheral Battery Monitor.lnk'
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $launcher
    $shortcut.WorkingDirectory = $install
    $shortcut.Description = 'Mouse and keyboard battery monitor'
    $shortcut.Save()
    $uninstall = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PeripheralBatteryMonitor'
    New-Item -Path $uninstall -Force | Out-Null
    $uninstallScript=Join-Path $destination 'installer\Uninstall.ps1'
    $properties = @{ DisplayName='Peripheral Battery Monitor'; DisplayVersion=$version; Publisher='vhnatovskyi'; InstallLocation=$install; DisplayIcon=$launcher; URLInfoAbout='https://github.com/vhnatovskyi/peripheral-battery-monitor'; UninstallString=('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+$uninstallScript+'" -InstallRoot "'+$install+'"') }
    foreach ($key in $properties.Keys) { New-ItemProperty -Path $uninstall -Name $key -Value $properties[$key] -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $uninstall -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstall -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
}
if (-not $NoLaunch) { Start-Process -FilePath $launcher }
if ($Interactive) { Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.MessageBox]::Show("Peripheral Battery Monitor $version is installed. Look for its battery icon near the clock.", 'Installation complete') | Out-Null }
"Installed $version in $install"
