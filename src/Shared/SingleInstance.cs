using System;
using System.Threading;

namespace PeripheralBatteryMonitor
{
    // Shared by the lightweight launcher and native application, including
    // installations in other folders and older compatible versions.
    internal static class SingleInstance
    {
        internal const string MutexName = @"Local\AJ139BatteryIndicator";
        private static Mutex owner;
        internal static bool TryClaim()
        {
            if(owner != null)return true;
            var candidate=new Mutex(false,MutexName);
            bool acquired;
            try { acquired=candidate.WaitOne(0); } catch(AbandonedMutexException) { acquired=true; }
            if(acquired) { owner=candidate;return true; }
            candidate.Dispose();SignalExisting();return false;
        }
        internal static bool ActivateExisting()
        {
            try
            {
                using(var existing=Mutex.OpenExisting(MutexName))
                {
                    bool available;
                    try { available=existing.WaitOne(0); } catch(AbandonedMutexException) { available=true; }
                    if(available) { existing.ReleaseMutex();return false; }
                }
                SignalExisting();return true;
            }
            catch(WaitHandleCannotBeOpenedException) { return false; }
        }
        private static void SignalExisting()
        {
            // A simultaneous launch can reach the mutex before MAUI has
            // created its activation event. Wait briefly for that event.
            for(int attempt=0;attempt<30;attempt++)
            {
                try { using(var show=EventWaitHandle.OpenExisting(@"Local\PeripheralBatteryMonitor.Show"))show.Set();return; }
                catch(WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
            }
        }
        internal static void Release()
        {
            if(owner==null)return;
            owner.ReleaseMutex();owner.Dispose();owner=null;
        }
    }
}
