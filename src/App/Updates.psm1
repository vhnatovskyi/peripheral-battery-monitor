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
    if((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $match.Groups[1].Value){throw 'Release checksum mismatch.'}
}
Export-ModuleMember -Function ConvertTo-PbmVersion,Test-PbmArchive,Assert-PbmChecksum
