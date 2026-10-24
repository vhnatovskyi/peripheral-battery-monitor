using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class SettingsPage : ContentPage
{
    private MonitorSettings draft=new();
    public SettingsPage(){AppRuntime.Current.SettingsChanged+=Build;Appearing+=(_,_)=>Build();Build();}
    private void Build()
    {
        draft=AppRuntime.Current.Settings.Value.Copy();
        var mode=new Picker { ItemsSource=new[]{Design.T("Одна спільна іконка","Combined icon"),Design.T("Окрема для кожного пристрою","One icon per device")},SelectedIndex=draft.TrayMode=="Separate"?1:0 };
        mode.SelectedIndexChanged+=(_,_)=>draft.TrayMode=mode.SelectedIndex==1?"Separate":"Combined";
        var language=new Picker { ItemsSource=new[]{"Українська","English"},SelectedIndex=draft.Language=="en"?1:0 };
        language.SelectedIndexChanged+=(_,_)=>draft.Language=language.SelectedIndex==1?"en":"uk";
        var startup=new Switch { IsToggled=draft.AutoStart };startup.Toggled+=(_,e)=>draft.AutoStart=e.Value;
        var updates=new Switch { IsToggled=draft.CheckForUpdates };updates.Toggled+=(_,e)=>draft.CheckForUpdates=e.Value;
        var alerts=new Switch { IsToggled=draft.LowBatteryAlerts };alerts.Toggled+=(_,e)=>draft.LowBatteryAlerts=e.Value;
        var threshold=Design.Label(draft.LowBatteryThreshold+"%");
        var slider=new Slider { Minimum=5,Maximum=50,Value=draft.LowBatteryThreshold,MinimumTrackColor=Color.FromArgb("#55DEAD") };
        slider.ValueChanged+=(_,e)=>{draft.LowBatteryThreshold=(int)Math.Round(e.NewValue);threshold.Text=draft.LowBatteryThreshold+"%";};
        var save=Design.Button(Design.T("Зберегти зміни","Save changes"),async(_,_)=>
        {try{AppRuntime.Current.SaveSettings(draft);await DisplayAlertAsync(Design.T("Збережено","Saved"),Design.T("Налаштування застосовано.","Settings applied."),"OK");}catch(Exception error){await DisplayAlertAsync(Design.T("Помилка","Error"),error.Message,"OK");}});
        var stack=Design.Stack(
            Design.Hint(Design.T("Підключені пристрої зʼявляються автоматично.","Connected devices appear automatically.")),
            Design.Panel(Design.Stack(Design.Label(Design.T("Вигляд і мова","Appearance and language"),16,true),Row(Design.T("Іконки в треї","Tray icons"),mode),Row(Design.T("Мова","Language"),language))),
            Design.Panel(Design.Stack(Design.Label(Design.T("Сповіщення","Notifications"),16,true),Row(Design.T("Низький заряд","Low battery"),alerts),Row(Design.T("Поріг сповіщення","Alert threshold"),threshold),slider)),
            Design.Panel(Design.Stack(Design.Label(Design.T("Програма","Application"),16,true),Row(Design.T("Запускати з Windows","Start with Windows"),startup),Row(Design.T("Перевіряти оновлення щодня","Check for updates daily"),updates))),save);
        stack.Padding=24;Content=new ScrollView { Content=stack };
    }
    private static Grid Row(string title,View control)
    {var row=new Grid { ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=16 };row.Add(Design.Label(title));row.Add(control,1);return row;}
}

