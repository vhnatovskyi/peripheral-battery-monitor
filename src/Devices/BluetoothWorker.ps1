param($Monitor)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Devices.Enumeration.DeviceInformation,Windows.Devices.Enumeration,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Enumeration.DeviceInformationCollection,Windows.Devices.Enumeration,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.BluetoothDevice,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.BluetoothLEDevice,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
[Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus,Windows.Devices.Bluetooth,ContentType=WindowsRuntime] | Out-Null
$asTask=[System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {$_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'} | Select-Object -First 1
function Await-Bluetooth($operation,[type]$type){
    $task=$asTask.MakeGenericMethod($type).Invoke($null,@($operation))
    if(-not $task.Wait(5000)){throw 'Bluetooth operation timed out'}
    $task.GetAwaiter().GetResult()
}
$sessions=@{};$attempted=@{}
$refreshVersion=$Monitor.RefreshVersion
try {
    while(-not $Monitor.IsStopped){
        try {
            if($refreshVersion -ne $Monitor.RefreshVersion){
                $refreshVersion=$Monitor.RefreshVersion
                foreach($session in $sessions.Values){if($session.Subscription){$session.Subscription.Dispose()};$session.Service.Dispose();$session.Device.Dispose()}
                $sessions.Clear();$attempted.Clear()
            }
            $endpoints=New-Object 'Collections.Generic.List[Aj139Battery.BluetoothEndpoint]'
            $connectedBle=@{}
            foreach($kind in @('Classic','BLE')){
                $selector=if($kind -eq 'Classic'){[Windows.Devices.Bluetooth.BluetoothDevice]::GetDeviceSelector()}else{[Windows.Devices.Bluetooth.BluetoothLEDevice]::GetDeviceSelector()}
                $items=Await-Bluetooth ([Windows.Devices.Enumeration.DeviceInformation]::FindAllAsync($selector,[string[]]@('System.Devices.Aep.IsConnected','System.Devices.Aep.ContainerId','System.Devices.Aep.DeviceAddress'),[Windows.Devices.Enumeration.DeviceInformationKind]::AssociationEndpoint)) ([Windows.Devices.Enumeration.DeviceInformationCollection])
                foreach($item in $items){
                    # WinRT dictionaries are projected differently across host versions.
                    $properties=@{};foreach($entry in $item.Properties){$properties[$entry.Key]=$entry.Value}
                    $id=[string]$properties['System.Devices.Aep.ContainerId']
                    if(-not $id){continue}
                    $endpoint=New-Object Aj139Battery.BluetoothEndpoint
                    $endpoint.Id=$id;$endpoint.Name=$item.Name;$endpoint.Connected=($properties['System.Devices.Aep.IsConnected'] -eq $true)
                    $endpoint.Address=[string]$properties['System.Devices.Aep.DeviceAddress']
                    $endpoints.Add($endpoint)
                    if($kind -ne 'BLE' -or -not $endpoint.Connected){continue}
                    $connectedBle[$id]=$true
                    if(-not $attempted.ContainsKey($id)){
                        $attempted[$id]=$true
                        $device=$null;$service=$null
                        try {
                            $device=Await-Bluetooth ([Windows.Devices.Bluetooth.BluetoothLEDevice]::FromIdAsync($item.Id)) ([Windows.Devices.Bluetooth.BluetoothLEDevice])
                            if(-not $device){continue}
                            $services=Await-Bluetooth ($device.GetGattServicesForUuidAsync([Guid]'0000180f-0000-1000-8000-00805f9b34fb',[Windows.Devices.Bluetooth.BluetoothCacheMode]::Uncached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattDeviceServicesResult])
                            foreach($candidate in $services.Services){if(-not $service){$service=$candidate}else{$candidate.Dispose()}}
                            if(-not $service){$device.Dispose();continue}
                            $chars=Await-Bluetooth ($service.GetCharacteristicsForUuidAsync([Guid]'00002a19-0000-1000-8000-00805f9b34fb')) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCharacteristicsResult])
                            $characteristic=$chars.Characteristics | Select-Object -First 1
                            if(-not $characteristic){$service.Dispose();$device.Dispose();continue}
                            $read=Await-Bluetooth ($characteristic.ReadValueAsync([Windows.Devices.Bluetooth.BluetoothCacheMode]::Uncached)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattReadResult])
                            $percent=if($read.Status -eq 'Success'){[Aj139Battery.BluetoothBuffer]::Battery($read.Value)}else{$null}
                            $source='Pbm.Battery.'+$id
                            $sessions[$id]=@{Device=$device;Service=$service;Characteristic=$characteristic;Percent=$percent;Updated=[DateTime]::UtcNow;Source=$source}
                            if($characteristic.CharacteristicProperties -band 16){
                                $sessions[$id].Subscription=New-Object Aj139Battery.BluetoothBatterySubscription($characteristic,$percent)
                                Await-Bluetooth ($characteristic.WriteClientCharacteristicConfigurationDescriptorAsync([Windows.Devices.Bluetooth.GenericAttributeProfile.GattClientCharacteristicConfigurationDescriptorValue]::Notify)) ([Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus]) | Out-Null
                            }
                        }catch{
                            $Monitor.SetError('Bluetooth battery: '+$_.Exception.Message)
                            if(-not $sessions.ContainsKey($id)){if($service){$service.Dispose()};if($device){$device.Dispose()}}
                        }
                    }
                }
            }
            foreach($id in @($sessions.Keys)){
                $session=$sessions[$id]
                if(-not $connectedBle.ContainsKey($id)){
                    if($session.Subscription){$session.Subscription.Dispose()}
                    Get-Event -SourceIdentifier $session.Source -ErrorAction SilentlyContinue | Remove-Event
                    $session.Service.Dispose();$session.Device.Dispose();$sessions.Remove($id);continue
                }
                if($session.Subscription){$session.Percent=$session.Subscription.Percent;$session.Updated=$session.Subscription.UpdatedUtc}
                foreach($endpoint in $endpoints){if($endpoint.Id -eq $id -and $endpoint.Connected){$endpoint.Percent=$session.Percent;$endpoint.UpdatedUtc=$session.Updated}}
            }
            foreach($id in @($attempted.Keys)){if(-not $connectedBle.ContainsKey($id)){$attempted.Remove($id)}}
            if(-not $Monitor.IsStopped){$Monitor.SetEndpoints($endpoints.ToArray())}
        }catch{$Monitor.SetError('Bluetooth discovery: '+$_.Exception.Message)}
        for($i=0;$i -lt 30 -and -not $Monitor.IsStopped;$i++){Start-Sleep -Milliseconds 100}
    }
}finally{
    foreach($session in $sessions.Values){
        if($session.Subscription){$session.Subscription.Dispose()}
        $session.Service.Dispose();$session.Device.Dispose()
    }
}
