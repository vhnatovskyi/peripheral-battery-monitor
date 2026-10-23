$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'Aj139Monitor.cs')
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

