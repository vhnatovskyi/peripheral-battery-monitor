using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using PeripheralBatteryMonitor.Desktop.Platforms.Windows;
using PeripheralBatteryMonitor.Desktop.Services;
using PeripheralBatteryMonitor.Interop;
using Microsoft.Win32;

namespace PeripheralBatteryMonitor.Agent;

internal sealed class AgentHost:IDisposable
{
    private readonly NativeHostWindow window=new();
    private readonly SettingsStore settings=new();
    private readonly DeviceService? devices;
    private readonly CancellationTokenSource stop=new();
    private readonly ConcurrentQueue<Action> actions=new();
    private readonly string instance=Guid.NewGuid().ToString("N");
    private readonly bool test;
    private readonly string pipeName;
    private readonly EventWaitHandle? exitEvent,showEvent;
    private readonly TrayHost? tray;
    private NativeBatteryWidget? widget;
    private AgentSnapshot snapshot=new();
    private DeviceReading[] readings=[];
    private readonly Dictionary<string,bool> alerted=[];
    private DateTime lastRead,persistDue;
    private bool pending,exiting,dark;
    private string? saveError;
    private long sequence;
    private long navigationSequence;
    private string route="devices";
    private IntPtr uiOwner;
    private int uiProcessId;
    private bool uiDetached;
    private Process? uiProcess;
    internal string PipeName=>pipeName;
    internal MonitorSettings Preferences=>settings.Value;
    internal NativeBatteryWidget? Widget=>widget;
    internal bool HasUiWindow=>uiOwner!=IntPtr.Zero;
    internal AgentHost(bool selfTest=false)
    {
        test=selfTest;pipeName=test?AgentWire.PipeName+".Test."+Guid.NewGuid().ToString("N"):AgentWire.PipeName;
        window.Message+=Message;
        if(!test)
        {
            exitEvent=new(false,EventResetMode.AutoReset,@"Local\PeripheralBatteryMonitor.Exit");
            showEvent=new(false,EventResetMode.AutoReset,@"Local\PeripheralBatteryMonitor.Show");
            tray=new(window.Handle,TrayAction);devices=new();devices.Start();
        }
        else readings=[new("test-mouse","Test mouse","2.4 GHz",90,"#55DEAD","Test",DateTime.UtcNow,null),new("test-keyboard","Test keyboard","Bluetooth",100,"#67BBFF","Test",DateTime.UtcNow,null),new("test-headset","Test headset","Bluetooth",82,"#A994EF","Test",DateTime.UtcNow,null)];
        RefreshTheme();UpdateWidget();Publish();_ = ServeAsync();
        if(!test && settings.Value.CheckForUpdates)_=CheckReleaseAsync();
    }
    internal void Run()=>window.Run();
    private void Message(uint message)
    {
        try
        {
            if(message==NativeHostWindow.Dispatch){while(actions.TryDequeue(out var action))action();}
            if(message is 0x1A or 0x31A){RefreshTheme();widget?.Update(readings);tray?.Invalidate();}
            if(message!=0x113 || exiting)return;
            if(exitEvent?.WaitOne(0)==true){Quit();return;}
            if(showEvent?.WaitOne(0)==true)ShowUi();
            CheckUiLifetime();
            widget?.Tick();
            if(DateTime.UtcNow-lastRead>=TimeSpan.FromMilliseconds(500))
            {
                lastRead=DateTime.UtcNow;
                var next=devices?.Snapshot().ToArray()??readings;
                bool changed=!readings.SequenceEqual(next);readings=next;
                tray?.Update(readings,settings.Value);widget?.Update(readings);if(changed)Publish();
                foreach(var device in readings.Where(d=>d.Percent.HasValue))
                {
                    if(device.Percent>settings.Value.LowBatteryThreshold+5)alerted[device.Id]=false;
                    if(settings.Value.LowBatteryAlerts && device.Percent<=settings.Value.LowBatteryThreshold && !alerted.GetValueOrDefault(device.Id))
                    {tray?.Notify(Text("Низький заряд","Low battery"),$"{device.Name}: {device.Percent}%");alerted[device.Id]=true;}
                }
            }
            if(pending && DateTime.UtcNow>=persistDue)Persist();
        }
        catch(Exception error){saveError=error.Message;Publish();}
    }
    private string Text(string uk,string en)=>settings.Value.Language=="uk"?uk:en;
    private void RefreshTheme()
    {
        bool systemDark=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value && value==0;
        dark=settings.Value.Theme=="Dark" || settings.Value.Theme=="System" && systemDark;
    }
    private IntPtr Owner()
    {
        if(uiDetached || uiOwner==IntPtr.Zero)return IntPtr.Zero;
        GetWindowThreadProcessId(uiOwner,out uint actual);
        return actual==(uint)uiProcessId?uiOwner:IntPtr.Zero;
    }
    private void CheckUiLifetime()
    {
        if(uiOwner==IntPtr.Zero)return;
        GetWindowThreadProcessId(uiOwner,out uint actual);
        if(actual==(uint)uiProcessId)return;
        uiOwner=IntPtr.Zero;uiProcessId=0;uiDetached=false;
        // Restore after the HWND has actually gone, rather than before its
        // closing/activation transitions. The widget belongs to this process.
        UpdateWidget();widget?.RestoreVisibility();
    }
    private void UpdateWidget()
    {
        if(settings.Value.WidgetEnabled)widget??=new(new(()=>settings.Value,()=>dark,Owner,SettingsStore.StateRoot));
        else{widget?.Dispose();widget=null;}
        widget?.Update(readings);
    }
    private void Apply(MonitorSettings value)
    {
        value=value.Copy();
        if(value.Language is not ("uk" or "en") || value.Theme is not ("System" or "Light" or "Dark") || value.TrayMode is not ("Separate" or "Combined") || value.WidgetMode is not ("Standard" or "Minimized") || value.WidgetOrientation is not ("Horizontal" or "Vertical"))throw new IOException("Invalid settings");
        value.WidgetOpacity=Math.Clamp(value.WidgetOpacity,0,100);value.LowBatteryThreshold=Math.Clamp(value.LowBatteryThreshold,5,50);
        value.WidgetHiddenDevices=value.WidgetHiddenDevices??[];value.TrayHiddenDevices=value.TrayHiddenDevices??[];
        settings.Apply(value);RefreshTheme();UpdateWidget();tray?.Invalidate();tray?.Update(readings,value);
        pending=true;persistDue=DateTime.UtcNow.AddMilliseconds(250);Publish();
    }
    private void Persist()
    {
        try{settings.Save(settings.Value,!test);pending=false;saveError=null;}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException){pending=false;saveError=error.Message;}
        Publish();
    }
    private void Publish()=>Volatile.Write(ref snapshot,new AgentSnapshot { Instance=instance,Sequence=++sequence,NavigationSequence=navigationSequence,Route=route,Settings=settings.Value,Devices=readings,SaveError=saveError });
    private async Task ServeAsync()
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,8,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                try{await pipe.WaitForConnectionAsync(stop.Token);_ = HandleClientAsync(pipe);}
                catch{pipe.Dispose();throw;}
            }
            catch(OperationCanceledException){break;}
            catch(IOException){try{await Task.Delay(250,stop.Token);}catch(OperationCanceledException){break;}}
        }
    }
    private async Task HandleClientAsync(NamedPipeServerStream pipe)
    {
        using(pipe)
        using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(4));
            try
            {
                var request=await AgentWire.ReadAsync(pipe,AgentJsonContext.Default.AgentRequest,timeout.Token);
                var response=await DispatchAsync(request).WaitAsync(timeout.Token);
                await AgentWire.WriteAsync(pipe,response,AgentJsonContext.Default.AgentResponse,timeout.Token);
            }
            catch(Exception error) when(error is IOException or OperationCanceledException or System.Text.Json.JsonException) { }
        }
    }
    private Task<AgentResponse> DispatchAsync(AgentRequest request)
    {
        var completion=new TaskCompletionSource<AgentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        actions.Enqueue(()=>
        {
            try
            {
                if(request.WindowHandle!=0)AttachUi(request);
                switch(request.Command)
                {
                    case "snapshot":break;
                    case "settings":Apply(request.Settings??throw new IOException("Settings missing"));break;
                    case "refresh":devices?.Refresh();break;
                    case "move-widget":widget?.BeginMove();break;
                    case "detach-ui":if(request.ProcessId==uiProcessId){uiDetached=true;widget?.SetMoving(false);}break;
                    default:throw new IOException("Unknown agent command");
                }
                var current=Volatile.Read(ref snapshot);
                bool unchanged=request.Command=="snapshot" && request.Instance==current.Instance && request.Sequence==current.Sequence;
                completion.SetResult(new() { Snapshot=unchanged?null:current,Error=current.SaveError });
            }
            catch(Exception error){completion.SetResult(new() { Success=false,Error=error.Message });}
        });window.Wake();return completion.Task;
    }
    private void AttachUi(AgentRequest request)
    {
        var handle=new IntPtr(request.WindowHandle);GetWindowThreadProcessId(handle,out uint actual);
        if(actual!=(uint)request.ProcessId)throw new IOException("Window owner mismatch");
        if(handle==uiOwner && request.ProcessId==uiProcessId)return;
        using var process=Process.GetProcessById(request.ProcessId);
        if(!string.Equals(process.MainModule?.FileName,Path.Combine(AppContext.BaseDirectory,"PeripheralBatteryMonitor.Desktop.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("Unexpected UI process");
        uiOwner=handle;uiProcessId=request.ProcessId;uiDetached=false;
    }
    private void TrayAction(string command)
    {
        if(command=="exit"){Quit();return;}
        if(command=="refresh"){devices?.Refresh();return;}
        ShowUi(command);
    }
    internal void ShowUi(string route="devices")
    {
        if(test)return;
        this.route=route;navigationSequence++;Publish();
        try{using var show=EventWaitHandle.OpenExisting(@"Local\PeripheralBatteryMonitor.UI.Show");show.Set();return;}catch(WaitHandleCannotBeOpenedException) { }
        if(uiProcess is { HasExited:false })return;
        uiProcess?.Dispose();
        var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"PeripheralBatteryMonitor.Desktop.exe")) { UseShellExecute=false,WorkingDirectory=AppContext.BaseDirectory,CreateNoWindow=true };
        start.ArgumentList.Add("--agent-client");start.ArgumentList.Add("--page="+route);uiProcess=Process.Start(start);
    }
    internal void Quit()
    {
        if(exiting)return;exiting=true;
        if(pending)Persist();
        if(!test)try{using var exit=EventWaitHandle.OpenExisting(@"Local\PeripheralBatteryMonitor.UI.Exit");exit.Set();}catch(WaitHandleCannotBeOpenedException) { }
        window.Quit();
    }
    private async Task CheckReleaseAsync()
    {
        try
        {
            string path=Path.Combine(SettingsStore.StateRoot,"maui-release-check.txt");
            if(File.Exists(path) && DateTime.UtcNow-File.GetLastWriteTimeUtc(path)<TimeSpan.FromDays(1))return;
            using var http=new System.Net.Http.HttpClient();http.DefaultRequestHeaders.UserAgent.ParseAdd("PeripheralBatteryMonitor/"+settings.Version);
            using var response=await http.GetStreamAsync("https://api.github.com/repos/vhnatovskyi/peripheral-battery-monitor/releases/latest",stop.Token);
            using var json=await System.Text.Json.JsonDocument.ParseAsync(response,cancellationToken:stop.Token);
            string? tag=json.RootElement.GetProperty("tag_name").GetString();
            Directory.CreateDirectory(SettingsStore.StateRoot);await File.WriteAllTextAsync(path,DateTime.UtcNow.ToString("O"),stop.Token);
            if(Version.TryParse(tag?.TrimStart('v'),out var version) && version>Version.Parse(settings.Version))
            {actions.Enqueue(()=>tray?.Notify(Text("Доступне оновлення","Update available"),tag+" — "+Text("відкрийте Оновлення","open Updates")));window.Wake();}
        }
        catch(Exception error) when(error is IOException or System.Net.Http.HttpRequestException or OperationCanceledException or System.Text.Json.JsonException) { }
    }
    internal Task<AgentResponse> RequestForTest(AgentRequest request)=>AgentWire.SendAsync(request,stop.Token,pipeName);
    internal void Post(Action action){actions.Enqueue(action);window.Wake();}
    public void Dispose(){stop.Cancel();widget?.Dispose();tray?.Dispose();window.Dispose();exitEvent?.Dispose();showEvent?.Dispose();uiProcess?.Dispose();stop.Dispose();}
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
}
