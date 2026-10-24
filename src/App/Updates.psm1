function ConvertTo-PbmVersion([string]$Text) {
    if ($Text -notmatch '^v?(\d+\.\d+\.\d+)$') { throw 'Unsupported release version.' }
    return [version]$Matches[1]
}
function Test-PbmArchive([string]$Path, [string]$Destination) {
    Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
    $root=[IO.Path]::GetFullPath($Destination).TrimEnd('\')+'\'
    $archive=[IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $total=0L
        foreach($entry in $archive.Entries){
            $target=[IO.Path]::GetFullPath((Join-Path $Destination $entry.FullName))
            if(-not $target.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive path.'}
            $total += $entry.Length
            if($total -gt 100MB){throw 'Release archive too large.'}
        }
    }finally{$archive.Dispose()}
}
function Assert-PbmChecksum([string]$Path,[string]$Manifest,[string]$Name) {
    $match=[regex]::Match($Manifest,'(?m)^([A-Fa-f0-9]{64})\s+\*?'+[regex]::Escape($Name)+'\s*$')
    if(-not $match.Success){throw 'Checksum missing from release.'}
    $sha=[Security.Cryptography.SHA256]::Create()
    $stream=[IO.File]::OpenRead([IO.Path]::GetFullPath($Path))
    try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}
    finally{$stream.Dispose();$sha.Dispose()}
    if(-not $actual.Equals($match.Groups[1].Value,[StringComparison]::OrdinalIgnoreCase)){throw 'Release checksum mismatch.'}
}
function Get-PbmCompatibleReleases($Releases){
    $valid=@(foreach($release in $Releases){
        if($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^v?\d+\.\d+\.\d+$'){continue}
        $version=ConvertTo-PbmVersion $release.tag_name
        $names=@($release.assets | ForEach-Object name)
        if($names -contains "PeripheralBatteryMonitor-$version-win-x64.zip" -and $names -contains 'SHA256SUMS.txt'){$release}
    })
    return @($valid | Sort-Object {ConvertTo-PbmVersion $_.tag_name} -Descending)
}
Export-ModuleMember -Function ConvertTo-PbmVersion,Test-PbmArchive,Assert-PbmChecksum,Get-PbmCompatibleReleases
