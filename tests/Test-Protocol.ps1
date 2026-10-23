$ErrorActionPreference = 'Stop'
Add-Type -Path @(Get-ChildItem (Join-Path $PSScriptRoot '../src/Core/*.cs'), (Join-Path $PSScriptRoot '../src/Devices/*.cs') | ForEach-Object FullName)
function Assert($condition, $message) { if (-not $condition) { throw $message } }
$value = 0; $flag = 0
$query = [byte[]]::new(65)
$query[1] = 0xAA; $query[2] = 0x30; $query[9] = 100
Assert ([Aj139Battery.Protocol]::TryBattery($query,65,[ref]$value,[ref]$flag)) 'Query response rejected'
Assert ($value -eq 100) 'Query percentage offset incorrect'
$event = [byte[]]::new(65)
$event[1] = 0xAA; $event[2] = 0xFA; $event[9] = 0xD0; $event[10] = 37; $event[11] = 1
Assert ([Aj139Battery.Protocol]::TryBattery($event,65,[ref]$value,[ref]$flag)) 'Battery event rejected'
Assert ($value -eq 37 -and $flag -eq 1) 'Event offsets incorrect'
$event[10] = 0
Assert ([Aj139Battery.Protocol]::TryBattery($event,65,[ref]$value,[ref]$flag)) 'Zero percent must be valid'
$event[10] = 255
Assert (-not [Aj139Battery.Protocol]::TryBattery($event,65,[ref]$value,[ref]$flag)) 'Invalid percentage accepted'
$event[10] = 37; $event[9] = 0x01
Assert (-not [Aj139Battery.Protocol]::TryBattery($event,65,[ref]$value,[ref]$flag)) 'DPI event treated as battery'
Assert (-not [Aj139Battery.Protocol]::TryBattery($query,9,[ref]$value,[ref]$flag)) 'Truncated response accepted'
$connected = $false
$online = [byte[]]::new(65); $online[1]=0xAA; $online[2]=0xED; $online[9]=2
Assert ([Aj139Battery.Protocol]::TryOnline($online,65,[ref]$connected) -and $connected) 'Online reply incorrect'
$online[9]=0
Assert ([Aj139Battery.Protocol]::TryOnline($online,65,[ref]$connected) -and -not $connected) 'Offline reply incorrect'
$packet=[Aj139Battery.Protocol]::Request(0x30)
Assert ([BitConverter]::ToString($packet,0,11) -eq '00-55-30-A5-0B-2E-01-01-00-00-00') 'Battery request incorrect'
'Protocol tests passed'


$keyboardReport = [byte[]](0x90,0,64)
Assert ([Aj139Battery.MagicKeyboardProtocol]::TryBattery($keyboardReport,3,[ref]$value) -and $value -eq 64) 'Magic Keyboard percentage offset incorrect'
$keyboardReport[2]=0
Assert ([Aj139Battery.MagicKeyboardProtocol]::TryBattery($keyboardReport,3,[ref]$value) -and $value -eq 0) 'Keyboard zero percent rejected'
$keyboardReport[2]=255
Assert (-not [Aj139Battery.MagicKeyboardProtocol]::TryBattery($keyboardReport,3,[ref]$value)) 'Invalid keyboard percentage accepted'
$keyboardReport[0]=0x91; $keyboardReport[2]=50
Assert (-not [Aj139Battery.MagicKeyboardProtocol]::TryBattery($keyboardReport,3,[ref]$value)) 'Unrelated keyboard report accepted'
$keyboardReport[0]=0x90
Assert (-not [Aj139Battery.MagicKeyboardProtocol]::TryBattery($keyboardReport,2,[ref]$value)) 'Truncated keyboard report accepted'
'Keyboard protocol tests passed'
