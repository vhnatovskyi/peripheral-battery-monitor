using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Aj139Battery
{
    public sealed class BatteryState
    {
        public int? Percent { get; internal set; }
        public int ChargeFlag { get; internal set; }
        public bool Online { get; internal set; }
        public string Status { get; internal set; }
        public long Version { get; internal set; }
        public DateTime UpdatedUtc { get; internal set; }
    }


}
