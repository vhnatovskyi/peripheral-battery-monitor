using Microsoft.Extensions.DependencyInjection;

namespace PeripheralBatteryMonitor.Desktop;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
        UserAppTheme=AppTheme.Dark;
        if(!Services.AppRuntime.Current.ClaimInstance())Environment.Exit(0);
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
        var window=new Window(new AppShell()) { Title="Peripheral Battery Monitor v"+Services.AppRuntime.Current.Settings.Version, Width=760,Height=500,MinimumWidth=600,MinimumHeight=400 };
        window.Created+=(_,_)=>Services.AppRuntime.Current.Start(window);
        return window;
	}
}
