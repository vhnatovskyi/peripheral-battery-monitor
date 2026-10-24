using Microsoft.Extensions.DependencyInjection;

namespace PeripheralBatteryMonitor.Desktop;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
        Services.AppRuntime.Current.ApplyTheme();
        RequestedThemeChanged+=(_,_)=>Services.AppRuntime.Current.RefreshAppearance();
        if(!Services.AppRuntime.Current.ClaimInstance())Environment.Exit(0);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        var window=new Window(new AppShell()) { Title="Peripheral Battery Monitor v"+Services.AppRuntime.Current.Settings.Version, Width=720,Height=460,MinimumWidth=560,MinimumHeight=360 };
        var titleBar=new TitleBar { Title=window.Title,Icon="brand.png" };
        titleBar.SetAppThemeColor(VisualElement.BackgroundColorProperty,Color.FromArgb("#F3F6FA"),Color.FromArgb("#101820"));
        titleBar.SetAppThemeColor(TitleBar.ForegroundColorProperty,Color.FromArgb("#182435"),Colors.White);
        window.TitleBar=titleBar;
        window.Created+=(_,_)=>Services.AppRuntime.Current.Start(window);
        return window;
	}
}
