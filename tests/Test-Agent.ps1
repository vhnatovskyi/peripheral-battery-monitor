param([Parameter(Mandatory=$true)][string]$PackageZip)
$ErrorActionPreference='Stop'
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('PbmAgentTests-'+[Guid]::NewGuid().ToString('N'))
$original=$env:PBM_AGENT_TEST_ROOT
$process=$null
try{
    $package=Join-Path $temporary 'package'
    Expand-Archive -LiteralPath $PackageZip -DestinationPath $package
    $agent=Join-Path $package 'app/PeripheralBatteryMonitor.Agent.exe'
    $env:PBM_AGENT_TEST_ROOT=Join-Path $temporary 'state'
    $process=Start-Process -FilePath $agent -ArgumentList '--agent-self-test' -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit(15000)){$process.Kill();$process.WaitForExit();throw 'Agent test timed out'}
    $result=Get-Content -Raw (Join-Path $env:PBM_AGENT_TEST_ROOT 'agent-result.txt')
    if($process.ExitCode -ne 0 -or $result -notlike 'PASS:*'){throw $result}
    $result
}finally{
    $env:PBM_AGENT_TEST_ROOT=$original
    if($process){$process.Dispose()}
    $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PbmAgentTests-'
    if(([IO.Path]::GetFullPath($temporary)).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $temporary)){Remove-Item -LiteralPath $temporary -Recurse -Force}
}
