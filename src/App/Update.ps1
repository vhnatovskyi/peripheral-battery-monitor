[CmdletBinding()]
param([switch]$Quiet,[switch]$CheckOnly,[ValidateSet('uk','en')][string]$Language='uk')
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Updates.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Localization.psm1') -Force
function U([string]$text){Get-PbmText $text $Language}
$packageRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$metadata=Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'version.json') | ConvertFrom-Json
$stateRoot=Join-Path $env:LOCALAPPDATA 'PeripheralBatteryMonitor'
New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
$cachePath=Join-Path $stateRoot 'release.json'
$repo='vhnatovskyi/peripheral-battery-monitor'
if(-not $Quiet){Add-Type -AssemblyName System.Windows.Forms,System.Drawing;. (Join-Path $PSScriptRoot 'ReleasePicker.ps1')}
try {
    if($Quiet -and (Test-Path -LiteralPath $cachePath)){
        try {$old=Get-Content -Raw -LiteralPath $cachePath | ConvertFrom-Json;if(([DateTime]::UtcNow-[DateTime]::Parse($old.checkedUtc).ToUniversalTime()).TotalHours -lt 24){exit}}catch{}
    }
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $headers=@{Accept='application/vnd.github+json';'User-Agent'='PeripheralBatteryMonitor'}
    if($Quiet -or $CheckOnly){$release=Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers $headers -TimeoutSec 20}
    else{
        $allReleases=Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases?per_page=100" -Headers $headers -TimeoutSec 20
        $releases=@(Get-PbmCompatibleReleases $allReleases)
        if(-not $releases.Count){throw (U 'No compatible releases found.')}
        $release=$releases[0]
    }
    $latest=ConvertTo-PbmVersion $release.tag_name
    $current=ConvertTo-PbmVersion $metadata.version
    $cache=@{checkedUtc=[DateTime]::UtcNow.ToString('o');version=$latest.ToString();url="https://github.com/$repo/releases/latest";available=($latest -gt $current)}
    $pending=Join-Path $stateRoot ('release-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $cache | ConvertTo-Json | Set-Content -LiteralPath $pending -Encoding UTF8
    Move-Item -LiteralPath $pending -Destination $cachePath -Force
    if($CheckOnly -or $Quiet){exit}
    $release=Show-PbmReleasePicker $releases $current $Language
    if(-not $release){exit}
    $latest=ConvertTo-PbmVersion $release.tag_name
    $question=if($latest -lt $current){(U 'Rollback to {0}? Features from newer versions may be unavailable. The application will restart.') -f $latest}else{(U 'Install version {0}? The application will restart. Your settings will be kept.') -f $latest}
    if([Windows.Forms.MessageBox]::Show($question,(U 'Releases and rollback'),'YesNo','Question') -ne 'Yes'){exit}
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
        $manifestPath=Join-Path $temporary 'SHA256SUMS.txt'
        Invoke-WebRequest -UseBasicParsing -Uri $checksums[0].browser_download_url -OutFile $manifestPath -TimeoutSec 20
        $manifest=Get-Content -Raw -LiteralPath $manifestPath
        Assert-PbmChecksum $zip $manifest $name
        $extracted=Join-Path $temporary 'package'
        Test-PbmArchive $zip $extracted
        Expand-Archive -LiteralPath $zip -DestinationPath $extracted
        $downloaded=Get-Content -Raw -LiteralPath (Join-Path $extracted 'version.json') | ConvertFrom-Json
        if((ConvertTo-PbmVersion $downloaded.version) -ne $latest -or $downloaded.repository -ne $repo){throw 'Package metadata differs from release.'}
        & (Join-Path $extracted 'installer/Install.ps1') -PackageRoot $extracted
        [Windows.Forms.MessageBox]::Show(((U 'Installed version {0}.') -f $latest),'Peripheral Battery Monitor') | Out-Null
    }finally{
        $resolved=[IO.Path]::GetFullPath($temporary)
        $tempBase=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PeripheralBatteryMonitor-update-'
        if($resolved.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $resolved -Recurse -Force -ErrorAction SilentlyContinue}
    }
}catch{
    if(-not $Quiet){[Windows.Forms.MessageBox]::Show(((U 'Could not update. {0}') -f $_.Exception.Message),'Peripheral Battery Monitor','OK','Warning') | Out-Null}
    exit 1
}
