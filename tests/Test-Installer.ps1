param([Parameter(Mandatory=$true)][string]$PackageZip)
$ErrorActionPreference='Stop'
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('PbmInstallTests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try{
    $package=Join-Path $temporary 'package'
    Expand-Archive -LiteralPath $PackageZip -DestinationPath $package
    $root=Join-Path $temporary 'PeripheralBatteryMonitor'
    & (Join-Path $package 'installer/Install.ps1') -PackageRoot $package -InstallRoot $root -NoLaunch -NoIntegration
    $version=(Get-Content -Raw (Join-Path $package 'version.json') | ConvertFrom-Json).version
    if((Get-Content -Raw (Join-Path $root 'current.txt')).Trim() -ne $version){throw 'Initial version pointer incorrect'}
    $settings=Join-Path $temporary 'settings.json'
    Set-Content -LiteralPath $settings -Value '{"LowBatteryThreshold":15}'
    $settingsHash=(Get-FileHash -LiteralPath $settings).Hash
    $next=Join-Path $temporary 'upgrade';Copy-Item -LiteralPath $package -Destination $next -Recurse
    $metadata=Get-Content -Raw (Join-Path $next 'version.json') | ConvertFrom-Json
    $metadata.version='99.0.0';$metadata | ConvertTo-Json | Set-Content (Join-Path $next 'version.json') -Encoding UTF8
    & (Join-Path $next 'installer/Install.ps1') -PackageRoot $next -InstallRoot $root -NoLaunch -NoIntegration
    if((Get-Content -Raw (Join-Path $root 'current.txt')).Trim() -ne '99.0.0'){throw 'Upgrade pointer incorrect'}
    if(-not (Test-Path -LiteralPath (Join-Path $root "versions/$version/src/App/Tray.ps1"))){throw 'Previous version was removed'}
    if((Get-FileHash -LiteralPath $settings).Hash -ne $settingsHash){throw 'Settings were changed'}
    $bad=Join-Path $temporary 'bad';Copy-Item -LiteralPath $next -Destination $bad -Recurse
    Remove-Item -LiteralPath (Join-Path $bad 'src/App/Tray.ps1')
    $rejected=$false;try{& (Join-Path $bad 'installer/Install.ps1') -PackageRoot $bad -InstallRoot $root -NoLaunch -NoIntegration}catch{$rejected=$true}
    if(-not $rejected){throw 'Broken package accepted'}
    if((Get-Content -Raw (Join-Path $root 'current.txt')).Trim() -ne '99.0.0'){throw 'Broken package changed active version'}
    'Install, upgrade, retained version and invalid-package tests passed'
}finally{
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PbmInstallTests-'
    if(([IO.Path]::GetFullPath($temporary)).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $temporary -Recurse -Force}
}
