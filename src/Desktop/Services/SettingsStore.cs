using System.Text.Json;
using Microsoft.Win32;

namespace PeripheralBatteryMonitor.Desktop.Services;

public sealed class SettingsStore
{
    public static readonly string StateRoot = (Environment.GetCommandLineArgs().Contains("--agent-self-test") || Environment.GetCommandLineArgs().Contains("--agent-integration-test")) && Environment.GetEnvironmentVariable("PBM_AGENT_TEST_ROOT") is string testRoot
        ? Path.GetFullPath(testRoot) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PeripheralBatteryMonitor");
    public static string PackageRoot => Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.FullName;
    public static string? InstalledRoot
    {
        get => ResolveInstalledRoot(PackageRoot);
    }
    internal static string? ResolveInstalledRoot(string packageRoot)
    { var package=new DirectoryInfo(packageRoot);return package.Parent?.Name=="versions"?package.Parent.Parent!.FullName:null; }
    public static string LauncherPath
    {
        get
        {
            var root = InstalledRoot ?? PackageRoot;
            return Path.Combine(root, "PeripheralBatteryMonitor.exe");
        }
    }
    public MonitorSettings Value { get; private set; } = new();
    public string Version { get; } = "0.6.0";
    internal void Apply(MonitorSettings settings) => Value=settings.Copy();
    public SettingsStore()
    {
        try { Value = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(StateRoot,"settings.json")),AppJsonContext.Default.MonitorSettings) ?? new(); }
        catch (Exception error) when(error is IOException or JsonException or UnauthorizedAccessException) { }
        if(Value.Language is not ("uk" or "en")) Value.Language="uk";
        if(Value.Theme is not ("System" or "Light" or "Dark"))Value.Theme="System";
        if(Value.TrayMode is not ("Combined" or "Separate")) Value.TrayMode="Combined";
        if(Value.TrayStyle is not ("Battery" or "Percent")) Value.TrayStyle="Battery";
        Value.LowBatteryThreshold=Math.Clamp(Value.LowBatteryThreshold,5,50);
        Value.WidgetOpacity=Math.Clamp(Value.WidgetOpacity,0,100);
        if(Value.WidgetMode is not ("Standard" or "Minimized"))Value.WidgetMode="Standard";
        if(Value.WidgetOrientation is not ("Horizontal" or "Vertical"))Value.WidgetOrientation="Horizontal";
        Value.WidgetHiddenDevices=Value.WidgetHiddenDevices?.Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        Value.TrayHiddenDevices=Value.TrayHiddenDevices?.Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
    }
    public void Save(MonitorSettings settings,bool integration=true)
    {
        Directory.CreateDirectory(StateRoot);
        string pending=Path.Combine(StateRoot,"settings.pending");
        File.WriteAllText(pending,JsonSerializer.Serialize(settings,AppJsonContext.Default.MonitorSettings));
        if(integration)
        {
            using var run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if(settings.AutoStart) run.SetValue("PeripheralBatteryMonitor",$"\"{LauncherPath}\" --background");
            else run.DeleteValue("PeripheralBatteryMonitor",false);
        }
        File.Move(pending,Path.Combine(StateRoot,"settings.json"),true);
        Value=settings.Copy();
    }
    public string Text(string uk,string en) => Value.Language=="uk" ? uk : en;
}
