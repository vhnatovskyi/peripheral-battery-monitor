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

$classic=New-Object Aj139Battery.BluetoothEndpoint
$classic.Id='test-headset';$classic.Name='Test headset';$classic.Connected=$false
$ble=New-Object Aj139Battery.BluetoothEndpoint
$ble.Id='test-headset';$ble.Name='Test headset';$ble.Connected=$true;$ble.Percent=83;$ble.UpdatedUtc=[DateTime]::UtcNow
$merged=[Aj139Battery.WindowsDeviceMonitor]::MergeEndpoints(@(),@($classic,$ble))
Assert ($merged.Count -eq 1 -and $merged[0].Connected -and $merged[0].Percent -eq 83) 'Inactive Classic endpoint hid active BLE headset'
$ble.Connected=$false
$merged=[Aj139Battery.WindowsDeviceMonitor]::MergeEndpoints(@(),@($classic,$ble))
Assert ($merged[0].Connected -eq $false -and $null -eq $merged[0].Percent) 'Disconnected headset retained current battery'
'Dual-mode Bluetooth connection regression tests passed'

# A Bluetooth update must be visible without starting a hardware rescan,
# and endpoint merging must not modify the cached native inventory.
$ble.Connected=$true;$ble.Percent=83
$native=[Aj139Battery.WindowsDeviceMonitor]::MergeEndpoints(@(),@($ble))
$ble.Connected=$false
$disconnected=[Aj139Battery.WindowsDeviceMonitor]::MergeEndpoints($native,@($ble))
Assert ($native[0].Connected -eq $true -and $native[0].Percent -eq 83) 'Bluetooth merge mutated the native snapshot'
Assert ($disconnected[0].Connected -eq $false -and $null -eq $disconnected[0].Percent) 'Cached disconnect update lost'
$monitor=New-Object Aj139Battery.WindowsDeviceMonitor
try {
    Assert ($null -eq $monitor.BluetoothConnected('magic-keyboard','Magic Keyboard')) 'Initial connection must remain unknown before enumeration'
    $monitor.SetEndpoints(@())
    Assert ($monitor.BluetoothConnected('magic-keyboard','Magic Keyboard') -eq $false) 'Successful empty inventory must disconnect missing keyboard'
    $ble.Connected=$true;$ble.Percent=83
    $monitor.SetEndpoints(@($ble))
    Assert ($monitor.Snapshot()[0].Percent -eq 83) 'Bluetooth update requires a hardware rescan'
    $version=$monitor.Version
    $ble.Percent=82;$monitor.SetEndpoints(@($ble))
    Assert ($monitor.Snapshot()[0].Percent -eq 82 -and $monitor.Version -gt $version) 'Cached battery change did not publish'
    $version=$monitor.Version
    $ble.Address='001122334455';$monitor.SetEndpoints(@($ble))
    Assert ($monitor.Version -gt $version -and $monitor.Snapshot()[0].BluetoothAddress -eq '001122334455') 'Bluetooth address update did not publish'
    $ble.Connected=$false;$monitor.SetEndpoints(@($ble))
    Assert ($monitor.Snapshot()[0].Connected -eq $false -and $null -eq $monitor.Snapshot()[0].Percent) 'Cached disconnection retained charge'
} finally { $monitor.Dispose() }
'Cached Bluetooth updates and immutable inventory regression tests passed'
