[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot '../dist'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=(Get-Content -Raw -LiteralPath (Join-Path $root 'version.json') | ConvertFrom-Json).version
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid version.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stage=Join-Path $output ('staging-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
try {
    New-Item -ItemType Directory -Path (Join-Path $stage 'src') | Out-Null
    foreach($folder in @('Core','Devices','UI','App','Launcher')){Copy-Item -LiteralPath (Join-Path $root "src/$folder") -Destination (Join-Path $stage 'src') -Recurse}
    Copy-Item -LiteralPath (Join-Path $root 'installer') -Destination $stage -Recurse
    $icon=Join-Path $root 'assets/branding/app-icon.ico'
    Copy-Item -LiteralPath (Join-Path $root 'version.json') -Destination $stage
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if(-not (Test-Path -LiteralPath $compiler)){throw 'Windows .NET Framework compiler not available.'}
    $launcher=Join-Path $stage 'PeripheralBatteryMonitor.exe'
    $automation=[System.Management.Automation.PowerShell].Assembly.Location
    & $compiler /nologo /target:winexe /platform:x64 "/out:$launcher" "/win32icon:$icon" /reference:System.Windows.Forms.dll "/reference:$automation" /codepage:65001 (Join-Path $root 'src/Shared/ProductInfo.cs') (Join-Path $root 'src/Launcher/Program.cs')
    if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed.'}
    $zipName="PeripheralBatteryMonitor-$version-win-x64.zip"
    $zip=Join-Path $output $zipName
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $setup=Join-Path $output "PeripheralBatteryMonitor-Setup-$version.exe"
    & $compiler /nologo /target:winexe /platform:x64 "/out:$setup" "/win32icon:$icon" /reference:System.Windows.Forms.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /codepage:65001 "/resource:$zip,payload.zip" "/resource:$icon,app-icon.ico" (Join-Path $root 'src/Shared/ProductInfo.cs') (Join-Path $root 'installer/Setup.cs') (Join-Path $root 'installer/InstallationForm.cs') (Join-Path $root 'installer/InstallPaths.cs')
    if($LASTEXITCODE -ne 0){throw 'Setup compilation failed.'}
    @($zip,$setup) | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(),(Split-Path $_ -Leaf) } | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
    "Built version $version in $output"
}finally{
    if(([IO.Path]::GetFullPath($stage)).StartsWith($output.TrimEnd('\')+'\staging-',[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $stage -Recurse -Force}
}
