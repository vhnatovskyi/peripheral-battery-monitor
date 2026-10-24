$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../installer/Runtime.psm1') -Force
$state=Get-PbmRuntimeState
if($null -eq $state.PSObject.Properties['Ready']){throw 'Runtime detection has no ready state'}
if($state.DotnetRoot -and -not(Test-Path -LiteralPath (Join-Path $state.DotnetRoot 'dotnet.exe'))){throw 'Runtime detector returned an invalid .NET folder'}
$unavailable=Get-PbmRuntimeState -MinimumWindowsVersion '99999.0.0.0'
if($unavailable.WindowsReady){throw 'Runtime detector accepted an older Windows App Runtime'}
'Shared runtime detection tests passed without installing components'
