$ErrorActionPreference='Stop'
Add-Type -Path @(Get-ChildItem (Join-Path $PSScriptRoot '../src/Core/*.cs'),(Join-Path $PSScriptRoot '../src/Devices/*.cs') | ForEach-Object FullName)
function Assert($condition,$message){if(-not $condition){throw $message}}
Assert ([Aj139Battery.WindowsBatteryProtocol]::ParsePercent([byte[]](0),3) -eq 0) 'Zero percent rejected'
Assert ([Aj139Battery.WindowsBatteryProtocol]::ParsePercent([byte[]](100),3) -eq 100) 'Byte percentage rejected'
Assert ($null -eq [Aj139Battery.WindowsBatteryProtocol]::ParsePercent([byte[]](101),3)) 'Unknown status 101 treated as percentage'
Assert ([Aj139Battery.WindowsBatteryProtocol]::ParsePercent([byte[]](75,0,0,0),7) -eq 75) 'DWORD percentage rejected'
Assert ($null -eq [Aj139Battery.WindowsBatteryProtocol]::ParsePercent([byte[]](255,255,255,255),7)) 'Overflow accepted'
Assert ($null -eq [Aj139Battery.WindowsBatteryProtocol]::ParsePercent($null,3)) 'Missing value treated as zero'
Assert ($null -eq [Aj139Battery.WindowsBatteryProtocol]::ParsePercent([byte[]](50),0x12)) 'Text property treated as battery'
Assert ([Aj139Battery.WindowsBatteryProtocol]::IsSpecialized('HID\VID_A8A5&PID_2255','Mouse')) 'Mouse duplicated in fallback'
Assert ([Aj139Battery.WindowsBatteryProtocol]::IsSpecialized('HID\VID&0001004C_PID&029F','Keyboard')) 'Keyboard duplicated in fallback'
Assert (-not [Aj139Battery.WindowsBatteryProtocol]::IsSpecialized('HID\VID_1234&PID_5678','Other device')) 'Unknown device excluded'
$devices=[Aj139Battery.WindowsDeviceDiscovery]::Scan()
if($devices){
    Assert (@($devices | Group-Object Id | Where-Object Count -gt 1).Count -eq 0) 'Duplicate physical device groups'
    Assert (@($devices | Where-Object {$_.Connected -eq $false -and $null -ne $_.Percent}).Count -eq 0) 'Disconnected device has a current percentage'
}
'Discovery parser and local enumeration tests passed'
