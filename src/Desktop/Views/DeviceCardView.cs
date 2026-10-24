using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class DeviceCardView : Border
{
    private readonly Image icon=new() { WidthRequest=24,HeightRequest=24 };
    private readonly Label name=Design.Label("",13,true),transport=Design.Hint(""),percent=Design.Label("",30,true),time=Design.Hint("");
    private readonly ProgressBar charge=new() { HeightRequest=4,BackgroundColor=Color.FromArgb("#344457") };
    private readonly ImageButton disconnect=new() { Source="unplug.png",WidthRequest=28,HeightRequest=28,Padding=6,BackgroundColor=Colors.Transparent };
    private DeviceReading? reading;
    private string? iconKey,disconnectKey;
    public DeviceCardView()
    {
        StrokeShape=new RoundRectangle { CornerRadius=10 };Design.ThemePanel(this);Padding=10;HeightRequest=128;
        charge.SetAppThemeColor(VisualElement.BackgroundColorProperty,Color.FromArgb("#DCE4ED"),Color.FromArgb("#344457"));
        transport.FontSize=11;time.FontSize=10;
        var head=new Grid { ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=8 };
        head.Add(icon);head.Add(new VerticalStackLayout { Spacing=3,Children={name,transport} },1);head.Add(disconnect,2);
        name.LineBreakMode=LineBreakMode.TailTruncation;name.MaxLines=1;
        var stack=Design.Stack(head,percent,charge,time);stack.Spacing=5;Content=stack;
        disconnect.Clicked+=Disconnect;
        var pointer=new PointerGestureRecognizer();
        pointer.PointerEntered+=(_,_)=>{disconnect.BackgroundColor=Color.FromArgb(Design.IsDark?"#34485F":"#DFE9F5");disconnect.BorderColor=Color.FromArgb(Design.IsDark?"#7D95B2":"#7692AF");};
        pointer.PointerExited+=(_,_)=>{disconnect.BackgroundColor=Colors.Transparent;disconnect.BorderColor=Colors.Transparent;};
        disconnect.CornerRadius=6;disconnect.BorderWidth=1;disconnect.BorderColor=Colors.Transparent;disconnect.GestureRecognizers.Add(pointer);
    }
    public void Update(DeviceReading value)
    {
        // Replacing FileImageSource on every state tick restarts the native image load.
        string key=value.Kind+(Design.IsDark?"":"_light")+".png";
        if(iconKey!=key){icon.Source=key;iconKey=key;}
        string unplug=Design.IsDark?"unplug.png":"unplug_light.png";
        if(disconnectKey!=unplug){disconnect.Source=unplug;disconnectKey=unplug;}
        reading=value;name.Text=value.Name;transport.Text=value.Transport;
        percent.Text=value.Percent is int level?level+"%":"—";
        charge.Progress=(value.Percent??0)/100d;
        var accent=Color.FromArgb(value.Accent);
        charge.ProgressColor=Design.IsDark?accent:Color.FromRgb(accent.Red*.62f,accent.Green*.62f,accent.Blue*.62f);
        time.Text=value.Percent.HasValue?Design.T("Оновлено ","Updated ")+value.UpdatedUtc.ToLocalTime().ToString("HH:mm"):Design.T("Заряд недоступний","Battery unavailable");
        disconnect.IsVisible=value.CanDisconnect;
        ToolTipProperties.SetText(disconnect,Design.T("Відʼєднати Bluetooth","Disconnect Bluetooth"));
        SemanticProperties.SetDescription(this,value.Name+", "+percent.Text+", "+value.Transport);
    }
    private async void Disconnect(object? sender,EventArgs args)
    {
        var target=reading;if(target?.CanDisconnect!=true)return;
        var page=Shell.Current.CurrentPage;
        if(!await page.DisplayAlertAsync(Design.T("Відʼєднати пристрій?","Disconnect device?"),target.Name,Design.T("Відʼєднати","Disconnect"),Design.T("Скасувати","Cancel")))return;
        disconnect.IsEnabled=false;
        try
        {
            int result=await Aj139Battery.BluetoothConnection.DisconnectAsync(target.Address!);
            if(result!=0)throw new System.ComponentModel.Win32Exception(result);
            AppRuntime.Current.Refresh();
        }
        catch(Exception error)
        {
            if(await page.DisplayAlertAsync(Design.T("Не вдалося відʼєднати","Unable to disconnect"),error.Message,Design.T("Параметри Bluetooth","Bluetooth settings"),Design.T("Закрити","Close")))
                await Launcher.Default.OpenAsync("ms-settings:bluetooth");
        }
        finally{disconnect.IsEnabled=true;}
    }
}

