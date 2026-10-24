using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

internal static class AgentClientSmoke
{
    internal static async Task RunAsync(Microsoft.UI.Xaml.Window window)
    {
        string path=Path.Combine(SettingsStore.StateRoot,"agent-ui-result.txt");
        try
        {
            var runtime=AppRuntime.Current;
            for(int i=0;i<30 && runtime.Readings.Count!=3;i++)await Task.Delay(200);
            if(runtime.Readings.Count!=3 || runtime.Readings[0].Percent!=90)throw new InvalidOperationException("UI did not receive agent batteries");
            ((AppShell)Shell.Current).Navigate("settings");await Task.Delay(400);
            var settings=runtime.Settings.Value.Copy();settings.Language="en";settings.Theme="Light";settings.WidgetEnabled=true;
            settings.WidgetMode="Minimized";settings.WidgetOrientation="Vertical";settings.WidgetOpacity=25;
            runtime.ChangeSettings(settings);
            await Task.Delay(800);
            var stored=new SettingsStore().Value;
            if(stored.Language!="en" || stored.WidgetOrientation!="Vertical" || stored.WidgetOpacity!=25)throw new InvalidOperationException("UI settings did not reach the agent and disk");
            // Close while a final change is in flight; the closing handler must flush it.
            settings=runtime.Settings.Value.Copy();settings.WidgetOpacity=37;runtime.ChangeSettings(settings);
            File.WriteAllText(path,"PASS: MAUI receives batteries, applies settings through IPC and closes through its normal window handler");
            window.Close();
        }
        catch(Exception error){File.WriteAllText(path,error.ToString());Environment.Exit(1);}
    }
}
