$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach($file in Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {$_.Extension -in @('.ps1','.psm1') -and $_.FullName -notmatch '[\\/]dist[\\/]'}){
    $tokens=$null;$errors=$null
    [System.Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors) | Out-Null
    if($errors.Count -gt 0){throw ($file.FullName+': '+($errors | Out-String))}
}
foreach($test in @('Test-Protocol.ps1','Test-Updates.ps1','Test-Discovery.ps1','Test-ReleasePicker.ps1','Test-TrayPainter.ps1','Test-Runtime.ps1')){
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tests/$test")
    if($LASTEXITCODE -ne 0){throw "$test failed"}
}
& powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $root 'src/App/Tray.ps1') -SmokeTest
if($LASTEXITCODE -ne 0){throw 'Dashboard smoke test failed'}
'All checks passed'
