$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$id=[Guid]::NewGuid().ToString('N')
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('PbmInstanceTests-'+$id)
New-Item -ItemType Directory -Path $temporary | Out-Null
try {
    foreach($file in @('src/Shared/SingleInstance.cs','tests/SingleInstanceTests.cs')){
        $body=[IO.File]::ReadAllText((Join-Path $root $file)).Replace('Local\AJ139BatteryIndicator',('Local\PbmInstanceTests-'+$id)).Replace('Local\PeripheralBatteryMonitor.Show',('Local\PbmInstanceTests-'+$id+'.Show'))
        [IO.File]::WriteAllText((Join-Path $temporary (Split-Path $file -Leaf)),$body)
    }
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $test=Join-Path $temporary 'SingleInstanceTests.exe'
    & $compiler /nologo /target:exe "/out:$test" (Join-Path $temporary 'SingleInstance.cs') (Join-Path $temporary 'SingleInstanceTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Single-instance tests failed to compile'}
    & $test $temporary
    if($LASTEXITCODE -ne 0){throw 'Single-instance tests failed'}
} finally {
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PbmInstanceTests-'
    if(([IO.Path]::GetFullPath($temporary)).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $temporary -Recurse -Force}
}
