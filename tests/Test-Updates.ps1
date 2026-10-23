$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../src/App/Updates.psm1') -Force
function Assert($condition,$message){if(-not $condition){throw $message}}
Assert ((ConvertTo-PbmVersion 'v0.2.0') -gt (ConvertTo-PbmVersion '0.1.9')) 'Version ordering failed'
foreach($invalid in @('../bad','v1.0.0-beta','https://example.test','1.2')){
    $rejected=$false;try{ConvertTo-PbmVersion $invalid | Out-Null}catch{$rejected=$true}
    Assert $rejected "Invalid version accepted: $invalid"
}
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('PbmTests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
try{
    $file=Join-Path $temporary 'payload.txt';Set-Content -LiteralPath $file -Value 'release test'
    $hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    Assert-PbmChecksum $file "$hash  payload.txt" 'payload.txt'
    $rejected=$false;try{Assert-PbmChecksum $file (('0'*64)+'  payload.txt') 'payload.txt'}catch{$rejected=$true}
    Assert $rejected 'Checksum mismatch accepted'
    Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
    $zip=Join-Path $temporary 'bad.zip'
    $archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
    $entry=$archive.CreateEntry('../escape.txt');$archive.Dispose()
    $rejected=$false;try{Test-PbmArchive $zip (Join-Path $temporary 'extract')}catch{$rejected=$true}
    Assert $rejected 'Archive traversal accepted'
}finally{
    $base=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PbmTests-'
    if(([IO.Path]::GetFullPath($temporary)).StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $temporary -Recurse -Force}
}
'Update validation tests passed'
