param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=(Get-Content -Raw (Join-Path $root 'version.json') | ConvertFrom-Json).version
$zip=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) "PeripheralBatteryMonitor-$version-win-x64.zip"
if((Get-Item -LiteralPath $zip).Length -gt 20MB){throw 'Package exceeds legacy update download limit'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try{
    $names=@($archive.Entries | ForEach-Object {$_.FullName.Replace('\','/')})
    if($names -notcontains 'app/PeripheralBatteryMonitor.Desktop.exe'){throw 'Native MAUI executable missing'}
    if($names -contains 'app/hostfxr.dll' -or $names -contains 'app/coreclr.dll'){throw 'Shared runtime unexpectedly bundled'}
    if(($archive.Entries | Measure-Object Length -Sum).Sum -gt 100MB){throw 'Package exceeds legacy archive limit'}
}finally{$archive.Dispose()}
& (Join-Path $PSScriptRoot 'Test-Installer.ps1') -PackageZip $zip
'MAUI compact package tests passed'