namespace PeripheralBatteryMonitor.Agent;

internal static class Program
{
    [STAThread]private static int Main(string[] args)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        bool test=args.Contains("--agent-self-test");
        if(test && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PBM_AGENT_TEST_ROOT")))return 2;
        if(!test && !SingleInstance.TryClaim())return 0;
        try
        {
            using var host=new AgentHost(test);
            if(test) _=AgentSelfTest.RunAsync(host);
            if(args.Contains("--show-ui"))host.ShowUi();
            host.Run();
        }
        finally{if(!test)SingleInstance.Release();}
        return test?AgentSelfTest.ExitCode:0;
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
}
