using Microsoft.Extensions.Logging;

namespace PeripheralBatteryMonitor.Desktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping("CompactSwitch",(handler,_)=>
        {
            var control=handler.PlatformView;
            control.OnContent=null;control.OffContent=null;control.MinWidth=0;
            control.Resources["ToggleSwitchThemeMinWidth"]=0d;
            control.Resources["ToggleSwitchPreContentMargin"]=6d;
            control.Resources["ToggleSwitchPostContentMargin"]=6d;
        });
        Microsoft.Maui.Handlers.SliderHandler.Mapper.AppendToMapping("WholePercent",(handler,_)=>
        { handler.PlatformView.StepFrequency=1;handler.PlatformView.SmallChange=1;handler.PlatformView.LargeChange=10; });

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
