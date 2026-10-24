using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aj139Battery
{
    public sealed class DiscoveredDevice
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Transport { get; internal set; }
        public int? Percent { get; internal set; }
        public bool? Connected { get; internal set; }
        public string Status { get; internal set; }
        public DateTime CheckedUtc { get; internal set; }
        public string BluetoothAddress { get; internal set; }
        internal bool Eligible;
        internal bool Specialized;
        internal int NameScore;
        internal DiscoveredDevice Copy() { return (DiscoveredDevice)MemberwiseClone(); }
    }

    public sealed class BluetoothEndpoint
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Connected { get; set; }
        public int? Percent { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public string Address { get; set; }
    }

    public static class WindowsBatteryProtocol
    {
        public static int? ParsePercent(byte[] value, uint type)
        {
            if (value == null) return null;
            uint number;
            if (type == 3 && value.Length == 1) number = value[0];
            else if (type == 7 && value.Length == 4) number = BitConverter.ToUInt32(value, 0);
            else return null;
            return number <= 100 ? (int?)number : null;
        }

        public static bool IsSpecialized(string id, string name)
        {
            string text = ((id ?? "") + " " + (name ?? "")).ToLowerInvariant();
            return text.Contains("vid_a8a5&pid_2255") || text.Contains("vid&0001004c_pid&029f") ||
                text.Contains("vid&012717_pid&32b0") || text.Contains("aj139 v2 mc");
        }
    }

    // Reads Windows' local property cache, not vendor command channels or radio scans.
    public sealed class WindowsDeviceMonitor : IDisposable
    {
        private readonly object sync = new object();
        private readonly ManualResetEvent stopped = new ManualResetEvent(false);
        private readonly AutoResetEvent refresh = new AutoResetEvent(false);
        private Task worker;
        private DiscoveredDevice[] devices = new DiscoveredDevice[0];
        private long version;
        private long refreshVersion;
        private volatile bool stopping;
        private BluetoothEndpoint[] endpoints = new BluetoothEndpoint[0];
        private DiscoveredDevice[] knownDevices = new DiscoveredDevice[0];
        public string LastError { get; private set; }
        public bool IsStopped { get { return stopping; } }
        public long RefreshVersion { get { return Interlocked.Read(ref refreshVersion); } }
        public void SetError(string message) { LastError = message; }
        public void SetEndpoints(BluetoothEndpoint[] value)
        {
            lock (sync)
            {
                endpoints = value;
                // Bluetooth reports update the cached inventory directly. They
                // do not require another complete SetupAPI enumeration.
                PublishDevices();
            }
        }
        public string ResolveBluetoothAddress(string id, string name)
        {
            lock(sync)
            {
                var match = endpoints.FirstOrDefault(e => e.Connected && !String.IsNullOrEmpty(e.Address) &&
                    (e.Id == id || (!String.IsNullOrEmpty(name) && !String.IsNullOrEmpty(e.Name) && e.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)));
                if(match != null) return match.Address;
                return knownDevices.Where(d => !String.IsNullOrEmpty(d.BluetoothAddress) &&
                    !String.IsNullOrEmpty(name) && d.Name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).Select(d => d.BluetoothAddress).FirstOrDefault();
            }
        }
        public long Version { get { lock (sync) return version; } }
        public DiscoveredDevice[] Snapshot() { lock (sync) return devices.ToArray(); }
        public void Start()
        {
            if (worker != null) throw new InvalidOperationException("Already started");
            worker = Task.Run((Action)Run);
        }
        public void Refresh() { Interlocked.Increment(ref refreshVersion); refresh.Set(); }
        private void Run()
        {
            var waits = new WaitHandle[] { stopped, refresh };
            while (!stopped.WaitOne(0))
            {
                try
                {
                    var found = WindowsDeviceDiscovery.Scan(true);
                    lock (sync)
                    {
                        string oldKnownKey = Fingerprint(knownDevices);
                        knownDevices = found;
                        string newKnownKey = Fingerprint(knownDevices);
                        bool changed = PublishDevices();
                        if (!changed && oldKnownKey != newKnownKey) version++;
                    }
                }
                catch (Exception error) { LastError = error.Message; }
                if (WaitHandle.WaitAny(waits, 3000) == 0) break;
            }
        }
        private bool PublishDevices()
        {
            var found = MergeEndpoints(knownDevices.Where(d => !d.Specialized).ToArray(), endpoints);
            bool changed = Fingerprint(devices) != Fingerprint(found);
            devices = found;
            if (changed) version++;
            return changed;
        }
        public static DiscoveredDevice[] MergeEndpoints(DiscoveredDevice[] native, BluetoothEndpoint[] endpoints)
        {
            var result = native.ToDictionary(d => d.Id, d => d.Copy(), StringComparer.OrdinalIgnoreCase);
            foreach (var group in endpoints.GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                var active = group.Where(e => e.Connected).ToArray();
                var endpoint = active.FirstOrDefault(e => e.Percent.HasValue) ?? active.FirstOrDefault() ?? group.First();
                DiscoveredDevice device;
                if (!result.TryGetValue(group.Key, out device))
                {
                    if (String.IsNullOrWhiteSpace(endpoint.Name) || WindowsBatteryProtocol.IsSpecialized("", endpoint.Name)) continue;
                    device = new DiscoveredDevice { Id = group.Key, Name = endpoint.Name, Transport = "Bluetooth", CheckedUtc = DateTime.UtcNow };
                    result[group.Key] = device;
                }
                device.Connected = active.Length > 0;
                device.BluetoothAddress = endpoint.Address;
                if (!String.IsNullOrWhiteSpace(endpoint.Name)) device.Name = endpoint.Name;
                if (device.Connected == false) { device.Percent = null; device.Status = "Disconnected"; }
                else if (endpoint.Percent.HasValue)
                { device.Percent = endpoint.Percent; device.CheckedUtc = endpoint.UpdatedUtc; device.Status = "Live Bluetooth battery report"; }
                else device.Status = device.Percent.HasValue ? "Windows battery data" : "Connected; battery unavailable";
            }
            return result.Values.OrderBy(d => d.Name).ToArray();
        }
        private static string Fingerprint(DiscoveredDevice[] entries)
        { return String.Join("\n", entries.Select(d => d.Id + "|" + d.Name + "|" + d.Transport + "|" + d.Percent + "|" + d.Connected + "|" + d.Status + "|" + d.BluetoothAddress)); }
        public void Dispose()
        { stopping = true; stopped.Set(); if (worker != null) worker.GetAwaiter().GetResult(); refresh.Dispose(); stopped.Dispose(); }
    }

    public static class WindowsDeviceDiscovery
    {
        [ThreadStatic] private static byte[] propertyBuffer;
        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceInfo { public int Size; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)]
        private struct Key { public Guid Format; public uint Id; public Key(string format, uint id) { Format = new Guid(format); Id = id; } }
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime { public ushort Y, M, DayOfWeek, Day, H, Min, Sec, Ms; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BluetoothInfo
        {
            public int Size; public ulong Address; public uint Class;
            [MarshalAs(UnmanagedType.Bool)] public bool Connected;
            [MarshalAs(UnmanagedType.Bool)] public bool Remembered;
            [MarshalAs(UnmanagedType.Bool)] public bool Authenticated;
            public SystemTime LastSeen, LastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name;
        }
        [DllImport("BluetoothAPIs.dll")] private static extern uint BluetoothGetDeviceInfo(IntPtr radio, ref BluetoothInfo info);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SetupDiGetClassDevs(IntPtr guid, string enumerator, IntPtr window, uint flags);
        [DllImport("setupapi.dll")] private static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfo info);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode)] private static extern bool SetupDiGetDeviceInstanceId(IntPtr set, ref DeviceInfo info, StringBuilder id, uint size, out uint needed);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, EntryPoint = "SetupDiGetDevicePropertyW")]
        private static extern bool ReadProperty(IntPtr set, ref DeviceInfo info, ref Key key, out uint type, byte[] data, uint size, out uint needed, uint flags);
        [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        private static byte[] Property(IntPtr set, ref DeviceInfo info, Key key, out uint type)
        {
            uint needed;
            var data = propertyBuffer ?? (propertyBuffer = new byte[256]);
            bool read = ReadProperty(set, ref info, ref key, out type, data, (uint)data.Length, out needed, 0);
            if (!read && needed > data.Length && needed <= 65536)
            {
                data = propertyBuffer = new byte[needed];
                read = ReadProperty(set, ref info, ref key, out type, data, (uint)data.Length, out needed, 0);
            }
            if (!read || needed > data.Length) return null;
            var result = new byte[needed]; Array.Copy(data, result, needed); return result;
        }
        private static string Text(IntPtr set, ref DeviceInfo info, Key key)
        { uint type; var data = Property(set, ref info, key, out type); return data != null && type == 0x12 ? Encoding.Unicode.GetString(data).TrimEnd('\0') : ""; }
        private static int? Battery(IntPtr set, ref DeviceInfo info, Key key)
        { uint type; return WindowsBatteryProtocol.ParsePercent(Property(set, ref info, key, out type), type); }
        private static bool? Connected(IntPtr set, ref DeviceInfo info)
        {
            foreach (var key in new[] { new Key("A35996AB-11CF-4935-8B61-A6761081ECDF", 7), new Key("78C34FC8-104A-4ACA-9EA4-524D52996E57", 55) })
            { uint type; var data = Property(set, ref info, key, out type); if (data != null && type == 0x11 && data.Length == 1) return data[0] != 0; }
            return null;
        }

        public static DiscoveredDevice[] Scan() { return Scan(false); }
        public static DiscoveredDevice[] Scan(bool includeSpecialized)
        {
            var groups = new Dictionary<string, DiscoveredDevice>(StringComparer.OrdinalIgnoreCase);
            var identifier = new StringBuilder(2048);
            int infoSize = Marshal.SizeOf(typeof(DeviceInfo));
            var set = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, 6);
            if (set == new IntPtr(-1)) throw new System.ComponentModel.Win32Exception();
            try
            {
                for (uint index = 0; ; index++)
                {
                    var info = new DeviceInfo(); info.Size = infoSize;
                    if (!SetupDiEnumDeviceInfo(set, index, ref info)) break;
                    identifier.Length = 0; uint needed;
                    if (!SetupDiGetDeviceInstanceId(set, ref info, identifier, (uint)identifier.Capacity, out needed)) continue;
                    string id = identifier.ToString();
                    bool bluetooth = id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase) || id.IndexOf("BLUETOOTH", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool usb = id.StartsWith("USB\\VID_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("HID\\VID_", StringComparison.OrdinalIgnoreCase);
                    if (!bluetooth && !usb) continue;
                    bool btRoot = id.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase) || id.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase);
                    string classId = info.ClassGuid.ToString();
                    bool input = classId == "745a17a0-74d3-11d0-b6fe-00a0c90f57da" || classId == "4d36e96b-e325-11ce-bfc1-08002be10318" || classId == "4d36e96f-e325-11ce-bfc1-08002be10318";
                    string name = Text(set, ref info, new Key("A45C254E-DF1C-4EFD-8020-67D146A850E0", 14));
                    string busName = Text(set, ref info, new Key("540B947E-8B40-45BC-A8A2-6A0B894CBDA2", 4));
                    int score = !String.IsNullOrWhiteSpace(name) && !name.StartsWith("HID-", StringComparison.OrdinalIgnoreCase) ? 70 : 10;
                    if (btRoot && !String.IsNullOrWhiteSpace(name)) score = 100;
                    if (!String.IsNullOrWhiteSpace(busName) && score < 70) { name = busName; score = 60; }
                    if (String.IsNullOrWhiteSpace(name)) name = Text(set, ref info, new Key("A45C254E-DF1C-4EFD-8020-67D146A850E0", 2));
                    uint type; var container = Property(set, ref info, new Key("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C", 2), out type);
                    string groupId = container != null && container.Length == 16 && new Guid(container) != Guid.Empty ? new Guid(container).ToString() : id;
                    DiscoveredDevice device;
                    if (!groups.TryGetValue(groupId, out device))
                    { device = new DiscoveredDevice { Id = groupId, Name = name, Transport = bluetooth ? "Bluetooth" : "USB / receiver", CheckedUtc = DateTime.UtcNow }; groups[groupId] = device; }
                    if (score > device.NameScore) { device.Name = name; device.NameScore = score; }
                    device.Specialized |= WindowsBatteryProtocol.IsSpecialized(id, name);
                    var percent = Battery(set, ref info, new Key("104EA319-6EE2-4701-BD47-8DDBF425BBE5", 2)) ?? Battery(set, ref info, new Key("49CD1F76-5626-4B17-A4E8-18B4AA1A2213", 10));
                    if (percent.HasValue) device.Percent = percent;
                    var connected = Connected(set, ref info); if (connected.HasValue) device.Connected = connected;
                    if (btRoot && id.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(id, @"DEV_([0-9A-F]{12})", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            var bt = new BluetoothInfo(); bt.Size = Marshal.SizeOf(bt); bt.Address = Convert.ToUInt64(match.Groups[1].Value, 16);
                            if (BluetoothGetDeviceInfo(IntPtr.Zero, ref bt) == 0) device.Connected = bt.Connected;
                        }
                    }
                    device.Eligible |= btRoot || percent.HasValue || (usb && input);
                    if(btRoot)
                    {
                        var address = Regex.Match(id, @"DEV_([0-9A-F]{12})", RegexOptions.IgnoreCase);
                        if(address.Success) device.BluetoothAddress = address.Groups[1].Value;
                    }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            foreach (var item in groups.Values)
            {
                if (item.Connected == false) { item.Percent = null; item.Status = "Disconnected"; }
                else if (item.Percent.HasValue) item.Status = item.Connected == true ? "Windows battery data" : "Cached battery; connection unconfirmed";
                else item.Status = item.Connected == true ? "Battery unavailable; device provider needed" : "Detected; battery unavailable";
            }
            return groups.Values.Where(d => d.Eligible && (includeSpecialized || !d.Specialized)).OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ThenBy(d => d.Id).ToArray();
        }
    }
}
