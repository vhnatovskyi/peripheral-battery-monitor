[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$version=(Get-Content -Raw (Join-Path $root 'version.json') | ConvertFrom-Json).version
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package version'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'dist-maui'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$stage=Join-Path $output ('staging-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'app') -Force | Out-Null
try{
    & dotnet publish (Join-Path $root 'src/Desktop/PeripheralBatteryMonitor.Desktop.csproj') -c Release -r win-x64 --self-contained false -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=false -p:PublishReadyToRun=false -p:PublishTrimmed=false -o (Join-Path $stage 'app') -v quiet -clp:ErrorsOnly
    if($LASTEXITCODE -ne 0){throw 'MAUI publish failed'}
    $sourceFolder=Join-Path $stage 'src'
    New-Item -ItemType Directory -Path $sourceFolder | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'src/App') -Destination $sourceFolder -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'installer') -Destination $stage -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'version.json') -Destination $stage
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $icon=Join-Path $root 'assets/branding/app-icon.ico'
    $launcher=Join-Path $stage 'PeripheralBatteryMonitor.exe'
    $automation=[System.Management.Automation.PowerShell].Assembly.Location
    & $compiler /nologo /target:winexe /platform:x64 "/out:$launcher" "/win32icon:$icon" /reference:System.Windows.Forms.dll "/reference:$automation" /codepage:65001 (Join-Path $root 'src/Shared/ProductInfo.cs') (Join-Path $root 'src/Shared/SingleInstance.cs') (Join-Path $root 'src/Launcher/Program.cs')
    if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $portableName="PeripheralBatteryMonitor-$version-win-x64.zip"
    $portable=Join-Path $output $portableName
    if(Test-Path -LiteralPath $portable){Remove-Item -LiteralPath $portable}
    [IO.Compression.ZipFile]::CreateFromDirectory($stage,$portable,[IO.Compression.CompressionLevel]::Optimal,$false)
    $setup=Join-Path $output "PeripheralBatteryMonitor-Setup-$version.exe"
    & $compiler /nologo /target:winexe /platform:x64 "/out:$setup" "/win32icon:$icon" /reference:System.Windows.Forms.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Drawing.dll /reference:System.Runtime.Serialization.dll /codepage:65001 "/resource:$portable,payload.zip" "/resource:$icon,app-icon.ico" (Join-Path $root 'src/Shared/ProductInfo.cs') (Join-Path $root 'installer/Setup.cs') (Join-Path $root 'installer/InstallationForm.cs') (Join-Path $root 'installer/InstallPaths.cs')
    if($LASTEXITCODE -ne 0){throw 'Setup compilation failed'}
    @($portable,$setup) | ForEach-Object {'{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(),(Split-Path $_ -Leaf)} | Set-Content (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
    "Built MAUI $version in $output"
}finally{
    if(([IO.Path]::GetFullPath($stage)).StartsWith($output.TrimEnd('\')+'\staging-',[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $stage -Recurse -Force}
}
