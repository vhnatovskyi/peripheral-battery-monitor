[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$PackageRoot,
    [string]$InstallRoot,
    [switch]$Interactive,
    [switch]$NoLaunch,
    [switch]$NoIntegration
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Load-InstallPaths.ps1')
$package = (Resolve-Path -LiteralPath $PackageRoot).Path
if(-not $InstallRoot){$InstallRoot=[PeripheralBatteryMonitor.Installation.InstallPaths]::PreferredRoot()}
$install = [PeripheralBatteryMonitor.Installation.InstallPaths]::ValidateDestination($InstallRoot)
if($package.StartsWith($install+'\',[StringComparison]::OrdinalIgnoreCase) -or $install.StartsWith($package+'\',[StringComparison]::OrdinalIgnoreCase) -or $package -eq $install){throw 'The package and installation folders must not overlap.'}
$metadata = Get-Content -Raw -LiteralPath (Join-Path $package 'version.json') | ConvertFrom-Json
$version = [string]$metadata.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version.' }
if($metadata.PSObject.Properties['interface'] -and $metadata.interface -eq 'maui' -and -not(Test-Path -LiteralPath (Join-Path $package 'app/PeripheralBatteryMonitor.Desktop.exe') -PathType Leaf)){throw 'Native MAUI application files are missing.'}
foreach ($required in @('PeripheralBatteryMonitor.exe','src\App\Tray.ps1','installer\Uninstall.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $required) -PathType Leaf)) { throw "Package missing $required" }
}
if((Test-Path -LiteralPath (Join-Path $package 'app/PeripheralBatteryMonitor.Desktop.exe')) -and -not ($NoIntegration -and $NoLaunch)){
    Import-Module (Join-Path $package 'installer/Runtime.psm1') -Force
    $dotnetRoot=Initialize-PbmRuntime
    $env:DOTNET_ROOT_X64=$dotnetRoot;$env:DOTNET_ROOT=$dotnetRoot
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
$activeInstall=[PeripheralBatteryMonitor.Installation.InstallPaths]::PreferredRoot()
$isolatedTest=$NoIntegration -and $NoLaunch -and ($install -ne $activeInstall)
if(-not $isolatedTest){try { $exitEvent=[Threading.EventWaitHandle]::OpenExisting('Local\PeripheralBatteryMonitor.Exit'); $exitEvent.Set() | Out-Null; $exitEvent.Dispose() } catch [Threading.WaitHandleCannotBeOpenedException] { }}
$mutexName=if($isolatedTest){'Local\PeripheralBatteryMonitor.Test.'+[Guid]::NewGuid().ToString('N')}else{'Local\AJ139BatteryIndicator'}
$instanceMutex = New-Object Threading.Mutex($false, $mutexName)
try {
    try { $acquired = $instanceMutex.WaitOne(10000) } catch [Threading.AbandonedMutexException] { $acquired = $true }
    if (-not $acquired) { throw 'Please close Peripheral Battery Monitor and run the installer again.' }
    try {
        $launcher = Join-Path $install 'PeripheralBatteryMonitor.exe'
        $pendingLauncher=Join-Path $install 'launcher.pending'
        Copy-Item -LiteralPath (Join-Path $destination 'PeripheralBatteryMonitor.exe') -Destination $pendingLauncher -Force
        for($attempt=0;$attempt -lt 20;$attempt++){
            try{
                if(Test-Path -LiteralPath $launcher){[IO.File]::Replace($pendingLauncher,$launcher,(Join-Path $install 'launcher.previous'))}else{[IO.File]::Move($pendingLauncher,$launcher)}
                break
            }catch [IO.IOException]{if($attempt -eq 19){throw};Start-Sleep -Milliseconds 200}
        }
        $pointer = Join-Path $install 'current.txt'
        $pending = Join-Path $install 'current.pending'
        [IO.File]::WriteAllText($pending, $version)
        if (Test-Path -LiteralPath $pointer) { [IO.File]::Replace($pending, $pointer, (Join-Path $install 'previous.txt')) } else { [IO.File]::Move($pending, $pointer) }
    } finally { $instanceMutex.ReleaseMutex() }
} finally { $instanceMutex.Dispose() }
& (Join-Path $PSScriptRoot 'Remove-OldVersions.ps1') -InstallRoot $install -CurrentVersion $version
if (-not $NoIntegration) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Peripheral Battery Monitor.lnk'
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $launcher
    $shortcut.WorkingDirectory = $install
    $shortcut.Description = 'Mouse and keyboard battery monitor'
    $shortcut.IconLocation = (Join-Path $destination 'PeripheralBatteryMonitor.exe')+',0'
    $shortcut.Save()
    $uninstall = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PeripheralBatteryMonitor'
    New-Item -Path $uninstall -Force | Out-Null
    $uninstallScript=Join-Path $destination 'installer\Uninstall.ps1'
    $properties = @{ DisplayName='Peripheral Battery Monitor'; DisplayVersion=$version; Publisher='Vladyslav Hnatovskyi'; InstallLocation=$install; DisplayIcon=$launcher; URLInfoAbout='https://github.com/vhnatovskyi/peripheral-battery-monitor'; UninstallString=('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "'+$uninstallScript+'" -InstallRoot "'+$install+'"') }
    foreach ($key in $properties.Keys) { New-ItemProperty -Path $uninstall -Name $key -Value $properties[$key] -PropertyType String -Force | Out-Null }
    New-ItemProperty -Path $uninstall -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $uninstall -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
    $installedKiB=[int][Math]::Ceiling((Get-ChildItem -LiteralPath $install -Recurse -File | Measure-Object Length -Sum).Sum/1KB)
    New-ItemProperty -Path $uninstall -Name EstimatedSize -Value $installedKiB -PropertyType DWord -Force | Out-Null
    $run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $startup=Get-ItemProperty -Path $run -Name PeripheralBatteryMonitor -ErrorAction SilentlyContinue
    if($startup -and $startup.PeripheralBatteryMonitor){Set-ItemProperty -Path $run -Name PeripheralBatteryMonitor -Value ('"'+$launcher+'" --background')}
}
if (-not $NoLaunch) { Start-Process -FilePath $launcher }
if ($Interactive) { Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.MessageBox]::Show("Peripheral Battery Monitor $version is installed. Look for its battery icon near the clock.", 'Installation complete') | Out-Null }
"Installed $version in $install"
