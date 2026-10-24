namespace PeripheralBatteryMonitor.Desktop;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        AddSection("devices",new Views.DevicesPage());
        AddSection("settings",new Views.SettingsPage());
        AddSection("releases",new Views.ReleasesPage());
        AddSection("about",new Views.AboutPage());
        Services.AppRuntime.Current.NavigateRequested+=route=>{if(sections.TryGetValue(route,out var item))CurrentItem=item;};
        Services.AppRuntime.Current.SettingsChanged+=Translate;
        Translate();
	}
    private readonly Dictionary<string,FlyoutItem> sections=[];
    public void Navigate(string route)=>CurrentItem=sections[route];
    private void AddSection(string route,ContentPage page)
    {
        var item=new FlyoutItem { Route=route };
        item.Items.Add(new ShellContent { Content=page });
        sections.Add(route,item);Items.Add(item);
    }
    private void Translate()
    {
        var runtime=Services.AppRuntime.Current;
        sections["devices"].Title=runtime.Text("Пристрої","Devices");
        sections["settings"].Title=runtime.Text("Налаштування","Settings");
        sections["releases"].Title=runtime.Text("Оновлення","Updates");
        sections["about"].Title=runtime.Text("Про програму","About");
        foreach(var item in sections.Values)((ContentPage)item.Items[0].Items[0].Content).Title=item.Title;
    }
}
