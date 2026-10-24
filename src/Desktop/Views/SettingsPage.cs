using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class SettingsPage : ContentPage
{
    private MonitorSettings draft=new();
    private VerticalStackLayout widgetDevices=new() { Spacing=4 };
    private string deviceFingerprint="";
    private Label saveStatus=Design.Hint("");
    private void Apply() => AppRuntime.Current.ChangeSettings(draft);
    public SettingsPage(){AppRuntime.Current.SettingsChanged+=Build;AppRuntime.Current.SettingsSaveStatus+=error=>saveStatus.Text=error==null?"":Design.T("Не вдалося зберегти: ","Could not save: ")+error;AppRuntime.Current.Changed+=UpdateWidgetDevices;Appearing+=(_,_)=>Build();Build();}
    private void UpdateWidgetDevices()
    {
        var readings=AppRuntime.Current.Readings;
        string fingerprint=string.Join('|',readings.Select(d=>$"{d.Id}:{d.Name}"));
        if(deviceFingerprint==fingerprint)return;
        deviceFingerprint=fingerprint;widgetDevices.Children.Clear();
        var headings=DeviceRow(Design.Hint(Design.T("Пристрій","Device")),Design.Hint(Design.T("Віджет","Widget")),Design.Hint(Design.T("Трей","Tray")));
        widgetDevices.Add(headings);
        if(readings.Count==0)widgetDevices.Add(Design.Hint(Design.T("Підключіть пристрій, щоб обрати його тут.","Connect a device to choose it here.")));
        foreach(var device in readings)
        {
            var toggle=new Switch { IsToggled=draft.ShowInWidget(device.Id),AutomationId="WidgetDevice_"+device.Id };
            toggle.Toggled+=(_,e)=>
            {
                var hidden=draft.WidgetHiddenDevices.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if(e.Value)hidden.Remove(device.Id);else hidden.Add(device.Id);
                draft.WidgetHiddenDevices=hidden.ToArray();Apply();
            };
            var tray=new Switch { IsToggled=draft.ShowInTray(device.Id),AutomationId="TrayDevice_"+device.Id };
            tray.Toggled+=(_,e)=>
            {
                var hidden=draft.TrayHiddenDevices.ToHashSet(StringComparer.OrdinalIgnoreCase);
                if(e.Value)hidden.Remove(device.Id);else hidden.Add(device.Id);
                draft.TrayHiddenDevices=hidden.ToArray();Apply();
            };
            SemanticProperties.SetDescription(toggle,device.Name+" — "+Design.T("показ у віджеті","show in widget"));
            SemanticProperties.SetDescription(tray,device.Name+" — "+Design.T("показ у треї","show in tray"));
            widgetDevices.Add(DeviceRow(Design.Label(device.Name,13),toggle,tray));
        }
    }
    private void Build()
    {
        draft=AppRuntime.Current.Settings.Value.Copy();
        saveStatus=Design.Hint(saveStatus.Text);
        widgetDevices=new() { Spacing=4 };
        deviceFingerprint="\0";UpdateWidgetDevices();
        var mode=new Picker { ItemsSource=new[]{Design.T("Одна спільна іконка","Combined icon"),Design.T("Окрема для кожного пристрою","One icon per device")},SelectedIndex=draft.TrayMode=="Separate"?1:0 };
        mode.SelectedIndexChanged+=(_,_)=>{draft.TrayMode=mode.SelectedIndex==1?"Separate":"Combined";Apply();};
        var language=new Picker { ItemsSource=new[]{"Українська","English"},SelectedIndex=draft.Language=="en"?1:0 };
        language.SelectedIndexChanged+=(_,_)=>{draft.Language=language.SelectedIndex==1?"en":"uk";Apply();};
        var themes=new[]{"System","Light","Dark"};
        var theme=new Picker { ItemsSource=new[]{Design.T("Як у системі","System"),Design.T("Світла","Light"),Design.T("Темна","Dark")},SelectedIndex=Array.IndexOf(themes,draft.Theme) };
        theme.SelectedIndexChanged+=(_,_)=>{if(theme.SelectedIndex>=0){draft.Theme=themes[theme.SelectedIndex];Apply();}};
        var startup=new Switch { IsToggled=draft.AutoStart };startup.Toggled+=(_,e)=>{draft.AutoStart=e.Value;Apply();};
        var updates=new Switch { IsToggled=draft.CheckForUpdates };updates.Toggled+=(_,e)=>{draft.CheckForUpdates=e.Value;Apply();};
        var alerts=new Switch { IsToggled=draft.LowBatteryAlerts };alerts.Toggled+=(_,e)=>{draft.LowBatteryAlerts=e.Value;Apply();};
        var widget=new Switch { IsToggled=draft.WidgetEnabled,AutomationId="EnableBatteryWidget" };
        var move=Design.Button(Design.T("Перемістити віджет","Move widget"),async(_,_)=>
        {
            try { AppRuntime.Current.MoveWidget(); }
            catch(Exception error){await DisplayAlertAsync(Design.T("Помилка","Error"),error.Message,"OK");}
        });
        move.IsEnabled=draft.WidgetEnabled;
        widget.Toggled+=(_,e)=>{draft.WidgetEnabled=e.Value;move.IsEnabled=e.Value;Apply();};
        var transparency=Design.Label((100-draft.WidgetOpacity)+"%");
        var opacity=new Slider { Minimum=0,Maximum=100,Value=100-draft.WidgetOpacity,AutomationId="WidgetTransparency" };
        opacity.ValueChanged+=(_,e)=>{draft.WidgetOpacity=100-(int)Math.Round(e.NewValue);transparency.Text=(100-draft.WidgetOpacity)+"%";Apply();};
        var threshold=Design.Label(draft.LowBatteryThreshold+"%");
        var slider=new Slider { Minimum=5,Maximum=50,Value=draft.LowBatteryThreshold,MinimumTrackColor=Color.FromArgb("#55DEAD") };
        slider.ValueChanged+=(_,e)=>{draft.LowBatteryThreshold=(int)Math.Round(e.NewValue);threshold.Text=draft.LowBatteryThreshold+"%";Apply();};
        var stack=Design.Stack(
            Design.Hint(Design.T("Зміни застосовуються й зберігаються автоматично.","Changes apply and save automatically.")),
            Design.Panel(Design.Stack(Design.Label(Design.T("Вигляд і мова","Appearance and language"),14,true),Row(Design.T("Іконки в треї","Tray icons"),mode),Row(Design.T("Мова","Language"),language),Row(Design.T("Тема","Theme"),theme))),
            Design.Panel(Design.Stack(Row(Design.T("Віджет поверх вікон","Always-on-top widget"),widget),Row(Design.T("Прозорість фону","Background transparency"),transparency),opacity,Design.Hint(Design.T("Текст залишається чітким. Кліки проходять крізь віджет; переміщення дозволяється кнопкою нижче й блокується при переході до іншої програми.","Text stays opaque. Clicks pass through; enable moving below. Switching to another app locks it again.")),move)),
            Design.Panel(Design.Stack(Design.Label(Design.T("Видимість пристроїв","Device visibility"),14,true),Design.Hint(Design.T("Незалежний показ у віджеті та над годинником. Застосовується одразу.","Choose independently for the widget and tray. Applies immediately.")),widgetDevices)),
            Design.Panel(Design.Stack(Design.Label(Design.T("Сповіщення","Notifications"),16,true),Row(Design.T("Низький заряд","Low battery"),alerts),Row(Design.T("Поріг сповіщення","Alert threshold"),threshold),slider)),
            Design.Panel(Design.Stack(Design.Label(Design.T("Програма","Application"),16,true),Row(Design.T("Запускати з Windows","Start with Windows"),startup),Row(Design.T("Перевіряти оновлення щодня","Check for updates daily"),updates))));
        stack.Padding=16;
        stack.Add(saveStatus);Content=new ScrollView { Content=stack };
    }
    private static Grid Row(string title,View control)
    {var row=new Grid { ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=16 };row.Add(Design.Label(title));row.Add(control,1);return row;}
    private static Grid DeviceRow(View name,View widget,View tray)
    {
        var row=new Grid { ColumnDefinitions=[new(GridLength.Star),new(70),new(70)],ColumnSpacing=8 };
        name.VerticalOptions=widget.VerticalOptions=tray.VerticalOptions=LayoutOptions.Center;
        widget.HorizontalOptions=tray.HorizontalOptions=LayoutOptions.Center;
        row.Add(name);row.Add(widget,1);row.Add(tray,2);return row;
    }
}

