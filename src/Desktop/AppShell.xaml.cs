namespace PeripheralBatteryMonitor.Desktop;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        ItemTemplate=new DataTemplate(()=>{
            var label=Views.Design.Label("",14);label.SetBinding(Label.TextProperty,"Title");label.HorizontalTextAlignment=TextAlignment.Start;label.VerticalOptions=LayoutOptions.Center;
            return new Grid { Padding=new Thickness(12,0),HeightRequest=40,Children={label} };
        });
        AddSection("devices",()=>new Views.DevicesPage());
        AddSection("settings",()=>new Views.SettingsPage());
        AddSection("releases",()=>new Views.ReleasesPage());
        AddSection("about",()=>new Views.AboutPage());
        Services.AppRuntime.Current.NavigateRequested+=route=>{if(sections.TryGetValue(route,out var item))CurrentItem=item;};
        Services.AppRuntime.Current.SettingsChanged+=Translate;
        Navigated+=(_,_)=>Services.AppRuntime.Current.ResizeForCurrentPage();
        Translate();
	}
    private readonly Dictionary<string,FlyoutItem> sections=[];
    private readonly Dictionary<string,ContentPage> loadedPages=[];
    public void Navigate(string route)=>CurrentItem=sections[route];
    private void AddSection(string route,Func<ContentPage> create)
    {
        var item=new FlyoutItem { Route=route };
        item.Items.Add(new ShellContent { ContentTemplate=new DataTemplate(()=>
        {
            var page=create();page.Title=item.Title;loadedPages[route]=page;
            if(Shell.GetTitleView(page)==null){var heading=Views.Design.Label("",18,true);heading.SetBinding(Label.TextProperty,new Binding(nameof(ContentPage.Title),source:page));heading.VerticalOptions=LayoutOptions.Center;Shell.SetTitleView(page,new Grid { Padding=new Thickness(12,0),HeightRequest=36,Children={heading} });}
            return page;
        }) });
        sections.Add(route,item);Items.Add(item);
    }
    private void Translate()
    {
        var runtime=Services.AppRuntime.Current;
        sections["devices"].Title=runtime.Text("Пристрої","Devices");
        sections["settings"].Title=runtime.Text("Налаштування","Settings");
        sections["releases"].Title=runtime.Text("Оновлення","Updates");
        sections["about"].Title=runtime.Text("Про програму","About");
        foreach(var page in loadedPages)page.Value.Title=sections[page.Key].Title;
    }
}
