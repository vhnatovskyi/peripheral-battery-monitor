$ErrorActionPreference='Stop'
function Get-PbmRuntimeState {
    param([string]$MinimumWindowsVersion='8000.994.2142.0')
    $roots=@((Join-Path $env:ProgramFiles 'dotnet'),(Join-Path $env:LOCALAPPDATA 'PeripheralBatteryMonitor/Runtimes/dotnet'))
    $dotnet=$null
    foreach($root in $roots){
        if((Test-Path -LiteralPath (Join-Path $root 'dotnet.exe')) -and @(Get-ChildItem -LiteralPath (Join-Path $root 'shared/Microsoft.NETCore.App') -Directory -ErrorAction SilentlyContinue | Where-Object {$_.Name -match '^10\.0\.\d+$'}).Count -gt 0){$dotnet=$root;break}
    }
    $modules=Join-Path $PSHOME 'Modules'
    if($env:PSModulePath -notlike "$modules;*"){$env:PSModulePath=$modules+';'+$env:PSModulePath}
    Import-Module Appx -ErrorAction Stop
    $windows=@(Appx\Get-AppxPackage -Name 'Microsoft.WindowsAppRuntime.1.8' | Where-Object {$_.Architecture -eq 'X64' -and [version]$_.Version -ge [version]$MinimumWindowsVersion}).Count -gt 0
    [pscustomobject]@{DotnetRoot=$dotnet;WindowsReady=$windows;Ready=($null -ne $dotnet -and $windows)}
}
function Initialize-PbmRuntime {
    param([string]$MinimumWindowsVersion='8000.994.2142.0')
    $state=Get-PbmRuntimeState $MinimumWindowsVersion
    if($state.Ready){return $state.DotnetRoot}
    Add-Type -AssemblyName System.Windows.Forms
    $needed=@();if(-not $state.DotnetRoot){$needed+='.NET 10 Runtime (x64)'};if(-not $state.WindowsReady){$needed+='Windows App Runtime 1.8 (x64)'}
    $answer=[Windows.Forms.MessageBox]::Show(('Microsoft components required once: '+($needed -join ', ')+'. Download and install now?'),'Peripheral Battery Monitor',[Windows.Forms.MessageBoxButtons]::YesNo,[Windows.Forms.MessageBoxIcon]::Information)
    if($answer -ne [Windows.Forms.DialogResult]::Yes){throw 'Required Microsoft components were not installed. The previous application version remains active.'}
    [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
    $cache=Join-Path ([IO.Path]::GetTempPath()) ('PbmRuntime-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $cache | Out-Null
    try{
        if(-not $state.DotnetRoot){
            $releases=Invoke-RestMethod -Uri 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
            $latest=$releases.'latest-runtime'
            $release=@($releases.releases | Where-Object {$_.runtime.version -eq $latest})[0]
            $asset=@($release.runtime.files | Where-Object {$_.rid -eq 'win-x64' -and $_.name -like '*.zip'})
            if($asset.Count -ne 1 -or $asset[0].url -notlike 'https://builds.dotnet.microsoft.com/dotnet/*' -or $asset[0].hash -notmatch '^[a-fA-F0-9]{128}$'){throw 'Unexpected Microsoft runtime metadata'}
            $zip=Join-Path $cache 'dotnet.zip'
            Invoke-WebRequest -UseBasicParsing -Uri $asset[0].url -OutFile $zip -TimeoutSec 300
            $sha=[Security.Cryptography.SHA512]::Create();$inputStream=[IO.File]::OpenRead($zip)
            try{$hash=([BitConverter]::ToString($sha.ComputeHash($inputStream))).Replace('-','')}finally{$inputStream.Dispose();$sha.Dispose()}
            if(-not $hash.Equals([string]$asset[0].hash,[StringComparison]::OrdinalIgnoreCase)){throw 'Microsoft runtime checksum mismatch'}
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $staging=Join-Path $cache 'dotnet';[IO.Compression.ZipFile]::ExtractToDirectory($zip,$staging)
            $shared=Join-Path $env:LOCALAPPDATA 'PeripheralBatteryMonitor/Runtimes/dotnet'
            New-Item -ItemType Directory -Path $shared -Force | Out-Null
            Copy-Item -Path (Join-Path $staging '*') -Destination $shared -Recurse -Force
        }
        if(-not $state.WindowsReady){
            $installer=Join-Path $cache 'WindowsAppRuntimeInstall.exe'
            Invoke-WebRequest -UseBasicParsing -Uri 'https://aka.ms/windowsappsdk/1.8/1.8.260921001/windowsappruntimeinstall-x64.exe' -OutFile $installer -TimeoutSec 300
            $signature=Get-AuthenticodeSignature -LiteralPath $installer
            if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?:^|,\s*)O=Microsoft Corporation(?:,|$)'){throw 'Windows App Runtime installer has no valid Microsoft signature'}
            $process=Start-Process -FilePath $installer -ArgumentList '--quiet' -WindowStyle Hidden -PassThru -Wait
            if($process.ExitCode -notin @(0,3010)){throw ('Windows App Runtime installation failed: '+$process.ExitCode)}
        }
        $state=Get-PbmRuntimeState $MinimumWindowsVersion
        if(-not $state.Ready){throw 'Microsoft runtime setup is incomplete. Restart Windows and run Setup again.'}
        return $state.DotnetRoot
    }finally{
        $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\PbmRuntime-'
        if(([IO.Path]::GetFullPath($cache)).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $cache -Recurse -Force}
    }
}
Export-ModuleMember -Function Get-PbmRuntimeState,Initialize-PbmRuntime
