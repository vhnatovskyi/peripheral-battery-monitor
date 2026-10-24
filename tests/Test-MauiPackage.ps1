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
    $images=@($names | Where-Object {$_ -match '(?i)^app/.*\.(png|ico|jpg|svg)$'})
    if($images.Count -gt 5 -or ($names -notcontains 'app/brand.png' -and $names -notcontains 'app/brand.scale-100.png') -or $names -notcontains 'app/app-icon.ico'){throw 'Unexpected image assets; keep at most five actually used images'}
    if($names -contains 'app/hostfxr.dll' -or $names -contains 'app/coreclr.dll'){throw 'Shared runtime unexpectedly bundled'}
    if(($archive.Entries | Measure-Object Length -Sum).Sum -gt 100MB){throw 'Package exceeds legacy archive limit'}
}finally{$archive.Dispose()}
& (Join-Path $PSScriptRoot 'Test-Installer.ps1') -PackageZip $zip
$setup=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) "PeripheralBatteryMonitor-Setup-$version.exe"
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($setup)
if($info.CompanyName -ne 'Vladyslav Hnatovskyi' -or $info.ProductVersion -ne $version -or $info.Comments -notlike '*linkedin.com/in/vladyslav-hnatovskyi/*' -or $info.LegalCopyright -notlike '*vhnatovskyi@hotmail.com*'){throw 'Installer developer metadata missing'}
'MAUI compact package tests passed'
