namespace PeripheralBatteryMonitor.Desktop.Services;

public sealed class AppRuntime
{
    public static AppRuntime Current { get; } = new();
    public SettingsStore Settings { get; } = new();
    public DeviceService Devices { get; } = new();
    public ReleaseService Releases { get; } = new();
    public IReadOnlyList<DeviceReading> Readings { get; private set; } = [];
    public event Action? Changed;
    public event Action? SettingsChanged;
    public event Action<string>? NavigateRequested;
    public event Action? ShowRequested;
    private readonly bool smoke=Environment.GetCommandLineArgs().Contains("--ui-smoke");
    private Mutex? instance;
    private EventWaitHandle? exitEvent,showEvent;
    private IDispatcherTimer? timer;
    private Platforms.Windows.TrayHost? tray;
    private Microsoft.UI.Xaml.Window? nativeWindow;
    private readonly Dictionary<string,bool> alerted=[];
    private bool exiting;
    public bool Background => Environment.GetCommandLineArgs().Contains("--background");
    public string Text(string uk,string en)=>Settings.Text(uk,en);
    public bool ClaimInstance()
    {
        if(smoke){string? language=Environment.GetEnvironmentVariable("PBM_UI_SMOKE_LANGUAGE");if(language is "uk" or "en")Settings.Value.Language=language;return true;}
        instance=new Mutex(false,@"Local\AJ139BatteryIndicator");
        bool owned;try{owned=instance.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
        if(!owned)
        { try{using var existing=EventWaitHandle.OpenExisting(@"Local\PeripheralBatteryMonitor.Show");existing.Set();}catch(WaitHandleCannotBeOpenedException){}return false; }
        exitEvent=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\PeripheralBatteryMonitor.Exit");
        showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\PeripheralBatteryMonitor.Show");
        return true;
    }
    public void Start(Window window)
    {
        nativeWindow=(Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!;
        nativeWindow.Title="Peripheral Battery Monitor";
        string icon=Path.Combine(AppContext.BaseDirectory,"app-icon.ico");
        if(File.Exists(icon))nativeWindow.AppWindow.SetIcon(icon);
        nativeWindow.AppWindow.Closing+=(_,args)=>{if(!exiting){args.Cancel=true;nativeWindow.AppWindow.Hide();}};
        if(smoke)
        {
            Readings=[new("demo-mouse","AJ139 V2 MC","2.4 GHz",93,"#55DEAD","Demo",DateTime.UtcNow,null),
                new("demo-keyboard","Magic Keyboard","Bluetooth",100,"#67BBFF","Demo",DateTime.UtcNow,null),
                new("demo-headset","Xbox Wireless Headset","Bluetooth",82,"#A994EF","Demo",DateTime.UtcNow,null)];
            Changed?.Invoke();
            if(Environment.GetEnvironmentVariable("PBM_UI_SMOKE_ROOT") is string folder && folder.Length>0)
                _=Platforms.Windows.UiSmoke.RunAsync(nativeWindow,folder);
            return;
        }
        tray=new Platforms.Windows.TrayHost(nativeWindow,HandleTrayAction);
        Devices.Start();
        if(Environment.GetEnvironmentVariable("PBM_UI_LIVE_ROOT") is string liveFolder && liveFolder.Length>0)
            _=CaptureLiveAsync(liveFolder);
        timer=window.Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(500);
        timer.Tick+=(_,_)=>Tick();timer.Start();Tick();
        if(Background)nativeWindow.AppWindow.Hide();
        if(Settings.Value.CheckForUpdates)_=CheckReleaseAsync();
    }
    private void Tick()
    {
        if(exitEvent?.WaitOne(0)==true){Exit();return;}
        if(showEvent?.WaitOne(0)==true)Show();
        Readings=Devices.Snapshot();tray?.Update(Readings,Settings.Value);Changed?.Invoke();
        foreach(var device in Readings.Where(d=>d.Percent.HasValue))
        {
            if(device.Percent>Settings.Value.LowBatteryThreshold+5)alerted[device.Id]=false;
            if(Settings.Value.LowBatteryAlerts && device.Percent<=Settings.Value.LowBatteryThreshold && !alerted.GetValueOrDefault(device.Id))
            { tray?.Notify(Text("Низький заряд","Low battery"),$"{device.Name}: {device.Percent}%");alerted[device.Id]=true; }
        }
    }
    private async Task CaptureLiveAsync(string folder)
    {
        await Task.Delay(10000);
        await Platforms.Windows.UiSmoke.RunAsync(nativeWindow!,folder);
    }
    private async Task CheckReleaseAsync()
    {
        try
        {
            string path=Path.Combine(SettingsStore.StateRoot,"maui-release-check.txt");
            if(File.Exists(path) && DateTime.UtcNow-File.GetLastWriteTimeUtc(path)<TimeSpan.FromDays(1))return;
            var releases=await Releases.LoadAsync();Directory.CreateDirectory(SettingsStore.StateRoot);await File.WriteAllTextAsync(path,DateTime.UtcNow.ToString("O"));
            if(releases.FirstOrDefault() is { } latest && latest.Version>Version.Parse(Settings.Version))
                MainThread.BeginInvokeOnMainThread(()=>tray?.Notify(Text("Доступне оновлення","Update available"),$"{latest.Tag} — "+Text("відкрийте Релізи","open Releases")));
        }catch(Exception error) when(error is HttpRequestException or IOException or TaskCanceledException) { }
    }
    public void SaveSettings(MonitorSettings value)
    { Settings.Save(value);SettingsChanged?.Invoke();tray?.Invalidate();Tick(); }
    public void Refresh(){Devices.Refresh();}
    public void Show(){nativeWindow?.AppWindow.Show();nativeWindow?.Activate();ShowRequested?.Invoke();}
    private void HandleTrayAction(string action)
    {
        if(action=="exit"){Exit();return;}if(action=="refresh"){Refresh();return;}
        Show();NavigateRequested?.Invoke(action);
    }
    public void Exit()
    {
        if(exiting)return;exiting=true;timer?.Stop();tray?.Dispose();
        try{instance?.ReleaseMutex();}catch(ApplicationException){}
        instance?.Dispose();exitEvent?.Dispose();showEvent?.Dispose();
        // Process exit releases pending device reads without delaying an upgrade.
        Environment.Exit(0);
    }
}
