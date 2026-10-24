using System.Diagnostics;
using PeripheralBatteryMonitor.Interop;

namespace PeripheralBatteryMonitor.Desktop.Services;

internal sealed class AgentConnection:IDisposable
{
    private readonly CancellationTokenSource stop=new();
    private readonly object sync=new();
    private MonitorSettings? pending;
    private Task settingsTask=Task.CompletedTask;
    private bool draining;
    private long receivedSequence=-1;
    private string receivedInstance="";
    public bool HasPendingSettings { get { lock(sync)return pending!=null || draining; } }
    public event Action<AgentSnapshot>? Received;
    public event Action<string?>? Error;
    public static bool ForwardToAgent()
    {
        if(Environment.GetCommandLineArgs().Contains("--agent-client") || Environment.GetCommandLineArgs().Contains("--ui-smoke"))return false;
        string executable=Path.Combine(AppContext.BaseDirectory,"PeripheralBatteryMonitor.Agent.exe");
        if(!File.Exists(executable))return false;
        StartAgent(!Environment.GetCommandLineArgs().Contains("--background"));return true;
    }
    private static void StartAgent(bool show)
    {
        string executable=Path.Combine(AppContext.BaseDirectory,"PeripheralBatteryMonitor.Agent.exe");
        Process.Start(new ProcessStartInfo(executable,show?"--show-ui":"--background") { WorkingDirectory=AppContext.BaseDirectory,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden });
    }
    public void Start(IntPtr window)
    { _=PollAsync(window); }
    private async Task PollAsync(IntPtr window)
    {
        DateTime lastStart=DateTime.MinValue;
        try
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    string instance;long sequence;lock(sync){instance=receivedInstance;sequence=receivedSequence;}
                    var response=await AgentWire.SendAsync(new() { Command="snapshot",WindowHandle=window.ToInt64(),ProcessId=Environment.ProcessId,Instance=instance,Sequence=sequence },stop.Token);
                    Accept(response);lastStart=DateTime.UtcNow;
                }
                catch(Exception error) when(error is IOException or OperationCanceledException or UnauthorizedAccessException)
                {
                    if(stop.IsCancellationRequested)break;
                    Error?.Invoke(error.Message);
                    if(!Environment.GetCommandLineArgs().Contains("--agent-integration-test") && DateTime.UtcNow-lastStart>TimeSpan.FromSeconds(5)){StartAgent(false);lastStart=DateTime.UtcNow;}
                }
                await Task.Delay(500,stop.Token);
            }
        }
        catch(OperationCanceledException) { }
    }
    private void Accept(AgentResponse response)
    {
        if(!response.Success){Error?.Invoke(response.Error);return;}
        if(response.Snapshot is not { } snapshot){Error?.Invoke(response.Error);return;}
        lock(sync)
        {
            if(receivedInstance!=snapshot.Instance){receivedInstance=snapshot.Instance;receivedSequence=-1;}
            if(snapshot.Sequence<receivedSequence)return;
            receivedSequence=snapshot.Sequence;
        }
        Received?.Invoke(snapshot);Error?.Invoke(snapshot.SaveError);
    }
    public void QueueSettings(MonitorSettings settings)
    {
        lock(sync)
        {
            pending=settings.Copy();
            if(!draining){draining=true;settingsTask=Task.Run(DrainSettingsAsync);}
        }
    }
    private async Task DrainSettingsAsync()
    {
        while(!stop.IsCancellationRequested)
        {
            MonitorSettings? value;
            lock(sync){value=pending;pending=null;if(value==null){draining=false;return;}}
            try { Accept(await AgentWire.SendAsync(new() { Command="settings",Settings=value },stop.Token)); }
            catch(Exception error) when(error is IOException or OperationCanceledException or UnauthorizedAccessException)
            {
                Error?.Invoke(error.Message);
                lock(sync){pending??=value;}
                try{await Task.Delay(500,stop.Token);}catch(OperationCanceledException){return;}
            }
        }
    }
    public async Task FlushAsync()
    {
        DateTime deadline=DateTime.UtcNow.AddSeconds(4);
        while(true){Task current;lock(sync){if(!draining && pending==null)return;current=settingsTask;}var remaining=deadline-DateTime.UtcNow;if(remaining<=TimeSpan.Zero)throw new TimeoutException("Settings delivery timed out");await current.WaitAsync(remaining);}
    }
    public async Task CommandAsync(string command)
    {
        try { Accept(await AgentWire.SendAsync(new() { Command=command,ProcessId=Environment.ProcessId },stop.Token)); }
        catch(Exception error) when(error is IOException or OperationCanceledException or UnauthorizedAccessException){Error?.Invoke(error.Message);}
    }
    public void Dispose(){stop.Cancel();}
}
