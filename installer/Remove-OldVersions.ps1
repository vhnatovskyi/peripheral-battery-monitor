param([Parameter(Mandatory=$true)][string]$InstallRoot,[Parameter(Mandatory=$true)][string]$CurrentVersion)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Load-InstallPaths.ps1')
$install=[PeripheralBatteryMonitor.Installation.InstallPaths]::ValidateDestination($InstallRoot)
if(-not [PeripheralBatteryMonitor.Installation.InstallPaths]::IsOwnedRoot($install)){throw 'Cannot clean an unrecognized installation'}
$versions=[IO.Path]::GetFullPath((Join-Path $install 'versions'))
$current=(Get-Content -Raw -LiteralPath (Join-Path $install 'current.txt')).Trim()
if($current -ne $CurrentVersion -or $current -notmatch '^\d+\.\d+\.\d+$'){throw 'Active version changed during cleanup'}
$keep=@($current)
$previous=Join-Path $install 'previous.txt'
if(Test-Path -LiteralPath $previous){$keep+=((Get-Content -Raw -LiteralPath $previous).Trim())}
foreach($folder in Get-ChildItem -LiteralPath $versions -Directory){
    if($folder.Name -notmatch '^\d+\.\d+\.\d+$' -or $keep -contains $folder.Name){continue}
    $target=[IO.Path]::GetFullPath($folder.FullName)
    if([IO.Path]::GetDirectoryName($target) -ne $versions -or ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Invalid cleanup target'}
    $metadata=Join-Path $target 'version.json'
    if(-not(Test-Path -LiteralPath $metadata -PathType Leaf)){continue}
    try{$identity=Get-Content -Raw -LiteralPath $metadata | ConvertFrom-Json}catch{continue}
    if($identity.repository -ne 'vhnatovskyi/peripheral-battery-monitor' -or $identity.version -ne $folder.Name){continue}
    # Validate the whole owned tree again immediately before recursive removal;
    # never follow directory links or remove unknown folders.
    [PeripheralBatteryMonitor.Installation.InstallPaths]::ValidateDestination($install) | Out-Null
    try{Remove-Item -LiteralPath $target -Recurse -Force}catch [IO.IOException]{Write-Warning ('Old version is still in use: '+$folder.Name)}catch [UnauthorizedAccessException]{Write-Warning ('Cannot remove old version: '+$folder.Name)}
}
