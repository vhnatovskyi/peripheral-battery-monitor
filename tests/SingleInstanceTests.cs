using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using PeripheralBatteryMonitor;

internal static class SingleInstanceTests
{
    private static int Main(string[] args)
    {
        string folder=args[0];
        if(args.Length>1)
        {
            if(!SingleInstance.TryClaim())return 2;
            using(var show=new EventWaitHandle(false,EventResetMode.AutoReset,ShowName))
            {
                File.WriteAllText(Path.Combine(folder,"ready"),"");
                while(true)if(show.WaitOne(100))File.WriteAllText(Path.Combine(folder,"activated"),"");
            }
        }
        Process child=null;
        try
        {
            child=Process.Start(new ProcessStartInfo(typeof(SingleInstanceTests).Assembly.Location,"\""+folder+"\" owner") { UseShellExecute=false,CreateNoWindow=true });
            WaitFile(folder,"ready");
            if(SingleInstance.TryClaim())throw new Exception("Second instance acquired the application mutex");
            WaitFile(folder,"activated");
            File.Delete(Path.Combine(folder,"activated"));
            if(!SingleInstance.ActivateExisting())throw new Exception("Launcher did not detect the running application");
            WaitFile(folder,"activated");
            child.Kill();child.WaitForExit();
            if(!SingleInstance.TryClaim())throw new Exception("Cannot restart after unexpected process exit");
            SingleInstance.Release();
            Console.WriteLine("Single-instance, launcher activation and crash recovery tests passed");return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error);return 1; }
        finally { if(child!=null) { if(!child.HasExited){child.Kill();child.WaitForExit();}child.Dispose(); } }
    }
    // Replaced with an isolated test event by Test-SingleInstance.ps1.
    private const string ShowName=@"Local\PeripheralBatteryMonitor.Show";
    private static void WaitFile(string folder,string name)
    {
        for(int i=0;i<100;i++){if(File.Exists(Path.Combine(folder,name)))return;Thread.Sleep(50);}
        throw new Exception("Timed out waiting for "+name);
    }
}
