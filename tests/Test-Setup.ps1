param([string]$ScreenshotPath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('PbmSetupCompiler-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $test=Join-Path $temporary 'InstallerFormTests.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:InstallerFormTests /codepage:65001 "/out:$test" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Runtime.Serialization.dll "/win32icon:$(Join-Path $root 'assets/branding/app-icon.ico')" "/resource:$(Join-Path $root 'assets/branding/app-icon.ico'),app-icon.ico" (Join-Path $root 'src/Shared/ProductInfo.cs') (Join-Path $root 'installer/Setup.cs') (Join-Path $root 'installer/InstallationForm.cs') (Join-Path $root 'installer/InstallPaths.cs') (Join-Path $PSScriptRoot 'InstallerFormTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Installer tests failed to compile'}
    if($ScreenshotPath){& $test $ScreenshotPath}else{& $test}
    if($LASTEXITCODE -ne 0){throw 'Installer tests failed'}
} finally {
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PbmSetupCompiler-'
    if(([IO.Path]::GetFullPath($temporary)).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $temporary -Recurse -Force}
}
