using Aj139Battery;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace PeripheralBatteryMonitor.Desktop.Services;

public sealed class DeviceService : IDisposable
{
    private readonly Aj139Battery.Monitor mouse=new();
    private readonly MagicKeyboardMonitor keyboard=new();
    private readonly WindowsDeviceMonitor windows=new();
    private readonly CancellationTokenSource stop=new();
    private readonly Dictionary<string,BleSession> sessions=new();
    private Task? bluetoothTask;
    public void Start() { mouse.Start();keyboard.Start();windows.Start();bluetoothTask=Task.Run(WatchBluetoothAsync); }
    public void Refresh() { mouse.Refresh();keyboard.Refresh();windows.Refresh(); }
    public IReadOnlyList<DeviceReading> Snapshot()
    {
        var readings=new List<DeviceReading>();
        var m=mouse.Snapshot();var k=keyboard.Snapshot();
        if(m.Online) readings.Add(new("aj139","AJ139 V2 MC","2.4 GHz",m.Percent,"#55DEAD","AJ139 HID",m.UpdatedUtc,null));
        if(k.Online) readings.Add(new("magic-keyboard","Magic Keyboard","Bluetooth",k.Percent,"#67BBFF","Apple HID",k.UpdatedUtc,windows.ResolveBluetoothAddress("magic-keyboard","Magic Keyboard")));
        foreach(var device in windows.Snapshot().Where(d=>d.Connected==true))
            readings.Add(new(device.Id,device.Name,device.Transport,device.Percent,"#A994EF",device.Status,device.CheckedUtc,device.BluetoothAddress));
        return readings;
    }
    private static int? ReadBuffer(IBuffer buffer)
    { if(buffer.Length!=1)return null;using var reader=DataReader.FromBuffer(buffer);byte value=reader.ReadByte();return value<=100 ? value : null; }
    private sealed class BleSession : IDisposable
    {
        public required BluetoothLEDevice Device; public required GattDeviceService Service; public required GattCharacteristic Characteristic;
        public int? Percent;public DateTime UpdatedUtc=DateTime.UtcNow;
        public void Changed(GattCharacteristic sender,GattValueChangedEventArgs args) { var value=ReadBuffer(args.CharacteristicValue);if(value.HasValue){Percent=value;UpdatedUtc=DateTime.UtcNow;} }
        public void Dispose() { Characteristic.ValueChanged-=Changed;Service.Dispose();Device.Dispose(); }
    }
    private async Task WatchBluetoothAsync()
    {
        long refreshVersion=windows.RefreshVersion;
        var attempted=new HashSet<string>();
        try
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    if(refreshVersion!=windows.RefreshVersion)
                    { refreshVersion=windows.RefreshVersion;foreach(var session in sessions.Values)session.Dispose();sessions.Clear();attempted.Clear(); }
                    var endpoints=new List<BluetoothEndpoint>();var activeBle=new HashSet<string>();
                    foreach(bool ble in new[]{false,true})
                    {
                        string selector=ble ? BluetoothLEDevice.GetDeviceSelector() : BluetoothDevice.GetDeviceSelector();
                        var devices=await DeviceInformation.FindAllAsync(selector,new[]{"System.Devices.Aep.IsConnected","System.Devices.Aep.ContainerId","System.Devices.Aep.DeviceAddress"},DeviceInformationKind.AssociationEndpoint);
                        foreach(var item in devices)
                        {
                            string? id=item.Properties.GetValueOrDefault("System.Devices.Aep.ContainerId")?.ToString();if(string.IsNullOrWhiteSpace(id))continue;
                            bool connected=item.Properties.GetValueOrDefault("System.Devices.Aep.IsConnected") is true;
                            var endpoint=new BluetoothEndpoint { Id=id,Name=item.Name,Connected=connected,Address=item.Properties.GetValueOrDefault("System.Devices.Aep.DeviceAddress")?.ToString() ?? "" };
                            endpoints.Add(endpoint);if(!ble || !connected)continue;activeBle.Add(id);
                            if(attempted.Add(id)) await OpenBatteryAsync(id,item.Id);
                            if(sessions.TryGetValue(id,out var session)) { endpoint.Percent=session.Percent;endpoint.UpdatedUtc=session.UpdatedUtc; }
                        }
                    }
                    foreach(string id in sessions.Keys.Where(id=>!activeBle.Contains(id)).ToArray()) { sessions[id].Dispose();sessions.Remove(id); }
                    attempted.RemoveWhere(id=>!activeBle.Contains(id));
                    if(!stop.IsCancellationRequested)windows.SetEndpoints(endpoints.ToArray());
                }
                catch(Exception error) when(error is not OperationCanceledException) { windows.SetError(error.Message); }
                await Task.Delay(3000,stop.Token);
            }
        }
        catch(OperationCanceledException) { }
        finally { foreach(var session in sessions.Values)session.Dispose();sessions.Clear(); }
    }
    private async Task OpenBatteryAsync(string id,string endpointId)
    {
        BluetoothLEDevice? device=null;GattDeviceService? service=null;
        try
        {
            device=await BluetoothLEDevice.FromIdAsync(endpointId);if(device is null)return;
            var services=await device.GetGattServicesForUuidAsync(Guid.Parse("0000180f-0000-1000-8000-00805f9b34fb"),BluetoothCacheMode.Uncached);
            service=services.Services.FirstOrDefault();foreach(var extra in services.Services.Skip(1))extra.Dispose();
            if(service is null){device.Dispose();return;}
            var chars=await service.GetCharacteristicsForUuidAsync(Guid.Parse("00002a19-0000-1000-8000-00805f9b34fb"));
            var characteristic=chars.Characteristics.FirstOrDefault();if(characteristic is null){service.Dispose();device.Dispose();return;}
            var read=await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            var session=new BleSession { Device=device,Service=service,Characteristic=characteristic,Percent=read.Status==GattCommunicationStatus.Success ? ReadBuffer(read.Value) : null };
            sessions[id]=session;
            if(characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            { characteristic.ValueChanged+=session.Changed;await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify); }
        }
        catch { if(!sessions.ContainsKey(id)){service?.Dispose();device?.Dispose();} }
    }
    public void Dispose()
    {
        stop.Cancel();mouse.Dispose();keyboard.Dispose();
        bluetoothTask?.GetAwaiter().GetResult();windows.Dispose();stop.Dispose();
    }
}
