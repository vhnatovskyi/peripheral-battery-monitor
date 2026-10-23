[CmdletBinding()]
param([switch]$Quiet,[switch]$CheckOnly)
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Updates.psm1') -Force
$packageRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$metadata=Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'version.json') | ConvertFrom-Json
$stateRoot=Join-Path $env:LOCALAPPDATA 'PeripheralBatteryMonitor'
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
$cachePath=Join-Path $stateRoot 'release.json'
$repo='vhnatovskyi/peripheral-battery-monitor'
if(-not $Quiet){Add-Type -AssemblyName System.Windows.Forms}
try {
    if($Quiet -and (Test-Path -LiteralPath $cachePath)){
        try {$old=Get-Content -Raw -LiteralPath $cachePath | ConvertFrom-Json;if(([DateTime]::UtcNow-[DateTime]::Parse($old.checkedUtc).ToUniversalTime()).TotalHours -lt 24){exit}}catch{}
    }
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $release=Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{Accept='application/vnd.github+json';'User-Agent'='PeripheralBatteryMonitor'} -TimeoutSec 20
    $latest=ConvertTo-PbmVersion $release.tag_name
    $current=ConvertTo-PbmVersion $metadata.version
    $cache=@{checkedUtc=[DateTime]::UtcNow.ToString('o');version=$latest.ToString();url="https://github.com/$repo/releases/latest";available=($latest -gt $current)}
    $pending=Join-Path $stateRoot ('release-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $cache | ConvertTo-Json | Set-Content -LiteralPath $pending -Encoding UTF8
    Move-Item -LiteralPath $pending -Destination $cachePath -Force
    if($CheckOnly -or $Quiet){exit}
    if($latest -le $current){[Windows.Forms.MessageBox]::Show("You have the latest version ($current).",'Peripheral Battery Monitor') | Out-Null;exit}
    if([Windows.Forms.MessageBox]::Show("Version $latest is available. Download and install it now? The application will restart.",'Peripheral Battery Monitor','YesNo','Question') -ne 'Yes'){exit}
    $name="PeripheralBatteryMonitor-$latest-win-x64.zip"
    $asset=@($release.assets | Where-Object name -eq $name)
    $checksums=@($release.assets | Where-Object name -eq 'SHA256SUMS.txt')
    if($asset.Count -ne 1 -or $checksums.Count -ne 1){throw 'Required release assets are missing.'}
    $prefix="https://github.com/$repo/releases/download/$($release.tag_name)/"
    foreach($item in @($asset[0],$checksums[0])){if(-not $item.browser_download_url.StartsWith($prefix,[StringComparison]::Ordinal)){throw 'Unexpected release download URL.'}}
    if($asset[0].size -gt 20MB){throw 'Release download too large.'}
    $temporary=Join-Path ([IO.Path]::GetTempPath()) ('PeripheralBatteryMonitor-update-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temporary | Out-Null
    try {
        $zip=Join-Path $temporary $name
        Invoke-WebRequest -UseBasicParsing -Uri $asset[0].browser_download_url -OutFile $zip -TimeoutSec 120
        $manifest=(Invoke-WebRequest -UseBasicParsing -Uri $checksums[0].browser_download_url -TimeoutSec 20).Content
        Assert-PbmChecksum $zip $manifest $name
        $extracted=Join-Path $temporary 'package'
        Test-PbmArchive $zip $extracted
        Expand-Archive -LiteralPath $zip -DestinationPath $extracted
        $downloaded=Get-Content -Raw -LiteralPath (Join-Path $extracted 'version.json') | ConvertFrom-Json
        if((ConvertTo-PbmVersion $downloaded.version) -ne $latest){throw 'Package version differs from release.'}
        & (Join-Path $extracted 'installer/Install.ps1') -PackageRoot $extracted
        [Windows.Forms.MessageBox]::Show("Updated to $latest.",'Peripheral Battery Monitor') | Out-Null
    }finally{
        $resolved=[IO.Path]::GetFullPath($temporary)
        $tempBase=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PeripheralBatteryMonitor-update-'
        if($resolved.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue}
    }
}catch{
    if(-not $Quiet){[Windows.Forms.MessageBox]::Show("Could not update. $($_.Exception.Message)",'Peripheral Battery Monitor','OK','Warning') | Out-Null}
    exit 1
}
