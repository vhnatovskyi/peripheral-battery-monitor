using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class ReleasesPage : ContentPage
{
    private readonly Picker picker=new();
    private readonly Label description=Design.Label(""),status=Design.Hint("");
    private readonly ProgressBar progress=new() { IsVisible=false };
    private readonly Button install=new(),refresh=new();
    private IReadOnlyList<ReleaseInfo> releases=[];
    private CancellationTokenSource? operation;
    public ReleasesPage()
    {
        description.LineBreakMode=LineBreakMode.WordWrap;
        var stack=Design.Stack(Design.Hint(Design.T("Поточна версія: ","Current version: ")+AppRuntime.Current.Settings.Version),picker,status,progress,new HorizontalStackLayout { Spacing=12,Children={install,refresh} },Design.Panel(description));
        stack.Padding=24;Content=new ScrollView { Content=stack };
        picker.SelectedIndexChanged+=(_,_)=>Selected();
        refresh.Clicked+=async(_,_)=>await Load();install.Clicked+=async(_,_)=>await Install();
        Appearing+=async(_,_)=>{Translate();if(releases.Count==0)await Load();};
        AppRuntime.Current.SettingsChanged+=Translate;Translate();
    }
    private void Translate(){refresh.Text=Design.T("Перевірити","Check releases");Selected();}
    private void Selected()
    {
        var release=picker.SelectedIndex>=0&&picker.SelectedIndex<releases.Count?releases[picker.SelectedIndex]:null;
        description.Text=release?.Notes??"";install.IsEnabled=release!=null&&operation==null;
        install.Text=release?.Version<Version.Parse(AppRuntime.Current.Settings.Version)?Design.T("Відкотити до цієї версії","Roll back to this version"):Design.T("Встановити версію","Install version");
    }
    private async Task Load()
    {
        refresh.IsEnabled=false;status.Text=Design.T("Завантаження релізів…","Loading releases…");
        try{releases=await AppRuntime.Current.Releases.LoadAsync();picker.ItemsSource=releases.Select(r=>r.Tag+" · "+r.Published.ToLocalTime().ToString("d")).ToArray();picker.SelectedIndex=releases.Count>0?0:-1;status.Text=releases.Count==0?Design.T("Релізів не знайдено","No releases found"):"";}
        catch(Exception error){status.Text=error.Message;}finally{refresh.IsEnabled=true;Selected();}
    }
    private async Task Install()
    {
        if(picker.SelectedIndex<0||operation!=null)return;var release=releases[picker.SelectedIndex];
        if(!await DisplayAlertAsync(Design.T("Встановити ","Install ")+release.Tag+"?",Design.T("Програма перезапуститься. Налаштування збережуться; старі версії можуть не підтримувати нові функції.","The app will restart. Settings are retained; older versions may not support newer features."),Design.T("Встановити","Install"),Design.T("Скасувати","Cancel")))return;
        operation=new();progress.IsVisible=true;install.IsEnabled=false;refresh.IsEnabled=false;picker.IsEnabled=false;
        try{await AppRuntime.Current.Releases.InstallAsync(release,new Progress<double>(value=>{progress.Progress=value;status.Text=Design.T("Завантаження та перевірка: ","Downloading and verifying: ")+(int)(value*100)+"%";}),operation.Token);status.Text=Design.T("Запуск інсталятора…","Starting installer…");}
        catch(Exception error){status.Text=error.Message;}
        finally{operation.Dispose();operation=null;refresh.IsEnabled=true;picker.IsEnabled=true;progress.IsVisible=false;Selected();}
    }
}

