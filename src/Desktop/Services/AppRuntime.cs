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
    public event Action? AppearanceChanged;
    public event Action<string>? NavigateRequested;
    public event Action? ShowRequested;
    private readonly bool smoke=Environment.GetCommandLineArgs().Contains("--ui-smoke");
    private readonly bool client=Environment.GetCommandLineArgs().Contains("--agent-client");
    private AgentConnection? agent;
    private bool closingClient;
    private long navigationSequence=-1;
    private long appliedSequence=-1;
    private string agentInstance="";
    private IDispatcherTimer? settingsSaveTimer;
    private bool settingsPending;
    public event Action<string?>? SettingsSaveStatus;
    private EventWaitHandle? exitEvent,showEvent;
    private IDispatcherTimer? timer;
    private Platforms.Windows.TrayHost? tray;
    private Platforms.Windows.BatteryWidget? widget;
    private Microsoft.UI.Xaml.Window? nativeWindow;
    private Window? mauiWindow;
    private ContentPage? fittedPage;
    private double fittedHeight;
    private readonly Dictionary<string,bool> alerted=[];
    private bool exiting;
    public bool Background => Environment.GetCommandLineArgs().Contains("--background");
    public string Text(string uk,string en)=>Settings.Text(uk,en);
    public bool ClaimInstance()
    {
        if(smoke){string? language=Environment.GetEnvironmentVariable("PBM_UI_SMOKE_LANGUAGE");if(language is "uk" or "en")Settings.Value.Language=language;return true;}
        if(!SingleInstance.TryClaim())return false;
        string prefix=client?SingleInstance.UiPrefix:@"Local\PeripheralBatteryMonitor";
        exitEvent=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+".Exit");
        showEvent=new EventWaitHandle(false,EventResetMode.AutoReset,prefix+".Show");
        return true;
    }
    public void Start(Window window)
    {
        mauiWindow=window;
        nativeWindow=(Microsoft.UI.Xaml.Window)window.Handler!.PlatformView!;
        nativeWindow.Title="Peripheral Battery Monitor v"+Settings.Version;
        UpdateCaptionTheme();
        if(nativeWindow.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)presenter.IsMaximizable=false;
        string icon=Path.Combine(AppContext.BaseDirectory,"app-icon.ico");
        if(File.Exists(icon))nativeWindow.AppWindow.SetIcon(icon);
        nativeWindow.AppWindow.Closing+=(sender,args)=>{if(!exiting){args.Cancel=true;if(client)_=CloseClientAsync();else nativeWindow.AppWindow.Hide();}};
        ResizeForCurrentPage();
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
        if(client)
        {
            agent=new();
            agent.Received+=snapshot=>window.Dispatcher.Dispatch(()=>Receive(snapshot));
            agent.Error+=error=>window.Dispatcher.Dispatch(()=>SettingsSaveStatus?.Invoke(error));
            agent.Start(WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow));
            timer=window.Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(250);
            timer.Tick+=(_,_)=>{if(exitEvent?.WaitOne(0)==true)Exit();else if(showEvent?.WaitOne(0)==true)Show();};timer.Start();
            if(Environment.GetCommandLineArgs().Contains("--agent-integration-test"))_=Platforms.Windows.AgentClientSmoke.RunAsync(nativeWindow);
            return;
        }
        tray=new Platforms.Windows.TrayHost(WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow),HandleTrayAction);
        SyncWidget();
        Devices.Start();
        if(Environment.GetEnvironmentVariable("PBM_UI_LIVE_ROOT") is string liveFolder && liveFolder.Length>0)
            _=CaptureLiveAsync(liveFolder);
        timer=window.Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(500);
        timer.Tick+=(_,_)=>Tick();timer.Start();Tick();
        if(Background)nativeWindow.AppWindow.Hide();
        if(Settings.Value.CheckForUpdates)_=CheckReleaseAsync();
    }
    private void Receive(Interop.AgentSnapshot snapshot)
    {
        if(exiting)return;
        if(agentInstance!=snapshot.Instance){agentInstance=snapshot.Instance;appliedSequence=-1;navigationSequence=-1;}
        if(snapshot.Sequence<appliedSequence)return;appliedSequence=snapshot.Sequence;
        Readings=snapshot.Devices;
        if(agent?.HasPendingSettings==false)
        {
            bool translate=Settings.Value.Language!=snapshot.Settings.Language;
            bool theme=Settings.Value.Theme!=snapshot.Settings.Theme;
            Settings.Apply(snapshot.Settings);
            if(theme)ApplyTheme();
            if(translate)SettingsChanged?.Invoke();
        }
        if(navigationSequence!=snapshot.NavigationSequence)
        {
            navigationSequence=snapshot.NavigationSequence;
            NavigateRequested?.Invoke(snapshot.Route);
        }
        Changed?.Invoke();
    }
    private async Task CloseClientAsync()
    {
        if(closingClient || exiting)return;closingClient=true;
        try
        {
            if(agent!=null){await agent.FlushAsync();await agent.CommandAsync("detach-ui");}
            Exit();
        }
        catch(Exception error) when(error is TimeoutException or IOException or OperationCanceledException)
        {closingClient=false;SettingsSaveStatus?.Invoke(error.Message);Show();}
    }
    private void Tick()
    {
        if(exitEvent?.WaitOne(0)==true){Exit();return;}
        if(showEvent?.WaitOne(0)==true)Show();
        Readings=Devices.Snapshot();tray?.Update(Readings,Settings.Value);widget?.Update(Readings);
        if(nativeWindow?.AppWindow.IsVisible==true)Changed?.Invoke();
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
    public void ChangeSettings(MonitorSettings value)
    {
        bool translate=Settings.Value.Language!=value.Language;
        bool theme=Settings.Value.Theme!=value.Theme;
        Settings.Apply(value);
        if(theme)ApplyTheme();
        SyncWidget();
        if(translate)SettingsChanged?.Invoke();
        tray?.Invalidate();tray?.Update(Readings,Settings.Value);
        if(smoke)return;
        if(client){agent?.QueueSettings(value);return;}
        settingsPending=true;
        settingsSaveTimer??=mauiWindow!.Dispatcher.CreateTimer();
        settingsSaveTimer.Interval=TimeSpan.FromMilliseconds(250);
        settingsSaveTimer.Tick-=SavePendingSettings;settingsSaveTimer.Tick+=SavePendingSettings;
        settingsSaveTimer.Stop();settingsSaveTimer.Start();
    }
    private void SavePendingSettings(object? sender,EventArgs args)
    {
        settingsSaveTimer?.Stop();
        if(!settingsPending)return;
        try { Settings.Save(Settings.Value);settingsPending=false;SettingsSaveStatus?.Invoke(null); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { SettingsSaveStatus?.Invoke(error.Message); }
    }
    private void SyncWidget()
    {
        if(nativeWindow==null || smoke || client)return;
        if(Settings.Value.WidgetEnabled)widget??=new Platforms.Windows.BatteryWidget(nativeWindow);
        else { widget?.Dispose();widget=null; }
        widget?.Update(Readings);
    }
    public void MoveWidget(){if(client)_=agent?.CommandAsync("move-widget");else widget?.BeginMove();}
    public void ApplyTheme()=>Application.Current!.UserAppTheme=Settings.Value.Theme switch { "Light"=>AppTheme.Light,"Dark"=>AppTheme.Dark,_=>AppTheme.Unspecified };
    public void RefreshAppearance(){UpdateCaptionTheme();widget?.Update(Readings);AppearanceChanged?.Invoke();Changed?.Invoke();tray?.Invalidate();}
    private void UpdateCaptionTheme()
    {
        if(nativeWindow==null)return;
        bool dark=Views.Design.IsDark;var bar=nativeWindow.AppWindow.TitleBar;
        bar.ButtonForegroundColor=dark?global::Windows.UI.Color.FromArgb(255,245,247,250):global::Windows.UI.Color.FromArgb(255,24,36,53);
        bar.ButtonInactiveForegroundColor=dark?global::Windows.UI.Color.FromArgb(255,149,163,184):global::Windows.UI.Color.FromArgb(255,82,100,122);
        bar.ButtonBackgroundColor=bar.ButtonInactiveBackgroundColor=global::Windows.UI.Color.FromArgb(0,0,0,0);
        bar.ButtonHoverBackgroundColor=dark?global::Windows.UI.Color.FromArgb(255,52,72,95):global::Windows.UI.Color.FromArgb(255,220,231,245);
        bar.ButtonHoverForegroundColor=bar.ButtonForegroundColor;
    }
    public void Refresh(){if(client)_=agent?.CommandAsync("refresh");else Devices.Refresh();}
    public double MaximumWindowHeight
    {
        get{
            if(nativeWindow==null)return 800;
            var display=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(nativeWindow.AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            double scale=((Microsoft.UI.Xaml.FrameworkElement)nativeWindow.Content).XamlRoot?.RasterizationScale??1;
            return display.WorkArea.Height/scale-24;
        }
    }
    public void ResizeForCurrentPage()
    {
        if(Shell.Current?.CurrentPage is not ContentPage page)return;
        if(page is Views.DevicesPage devices){devices.FitToDevices();return;}
        ResizeForPage(page,page is Views.SettingsPage?820:page is Views.ReleasesPage?560:470);
    }
    public void ResizeForPage(ContentPage page,double height)
    {
        if(mauiWindow==null || nativeWindow==null || Shell.Current?.CurrentPage!=page)return;
        double target=Math.Clamp(height,240,Math.Max(240,MaximumWindowHeight));
        if(fittedPage==page && Math.Abs(fittedHeight-target)<1)return;
        fittedPage=page;fittedHeight=target;mauiWindow.Height=target;
        var display=Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(nativeWindow.AppWindow.Id,Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        double scale=((Microsoft.UI.Xaml.FrameworkElement)nativeWindow.Content).XamlRoot?.RasterizationScale??1;
        var position=nativeWindow.AppWindow.Position;int bottom=display.WorkArea.Y+display.WorkArea.Height;
        if(position.Y+target*scale>bottom)nativeWindow.AppWindow.Move(new global::Windows.Graphics.PointInt32(position.X,Math.Max(display.WorkArea.Y,bottom-(int)Math.Ceiling(target*scale))));
    }
    public void Show(){nativeWindow?.AppWindow.Show();nativeWindow?.Activate();Changed?.Invoke();ShowRequested?.Invoke();}
    private void HandleTrayAction(string action)
    {
        if(action=="exit"){Exit();return;}if(action=="refresh"){Refresh();return;}
        Show();NavigateRequested?.Invoke(action);
    }
    public void Exit()
    {
        if(exiting)return;exiting=true;SavePendingSettings(null,EventArgs.Empty);timer?.Stop();widget?.Dispose();tray?.Dispose();agent?.Dispose();
        SingleInstance.Release();exitEvent?.Dispose();showEvent?.Dispose();
        // Process exit releases pending device reads without delaying an upgrade.
        Environment.Exit(0);
    }
}
