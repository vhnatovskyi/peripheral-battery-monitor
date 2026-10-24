using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class DeviceCardView : Border
{
    private readonly Label icon=new() { WidthRequest=24,HeightRequest=24,FontFamily=Design.IconFont,FontSize=24,VerticalTextAlignment=TextAlignment.Center,HorizontalTextAlignment=TextAlignment.Center };
    private readonly Label name=Design.Label("",13,true),transport=Design.Hint(""),percent=Design.Label("",30,true),time=Design.Hint("");
    private readonly ProgressBar charge=new() { HeightRequest=4,BackgroundColor=Color.FromArgb("#344457") };
    private readonly Button disconnect=new() { Text=Design.Glyph("unplug"),FontFamily=Design.IconFont,FontSize=16,WidthRequest=28,HeightRequest=28,Padding=0,BackgroundColor=Colors.Transparent };
    private DeviceReading? reading;
    private string? iconKey,disconnectKey;
    public DeviceCardView()
    {
        StrokeShape=new RoundRectangle { CornerRadius=10 };Design.ThemePanel(this);Padding=10;HeightRequest=88;
        charge.SetAppThemeColor(VisualElement.BackgroundColorProperty,Color.FromArgb("#DCE4ED"),Color.FromArgb("#344457"));
        transport.FontSize=11;time.FontSize=9;percent.FontSize=26;
        percent.HorizontalTextAlignment=TextAlignment.End;time.HorizontalTextAlignment=TextAlignment.End;
        var metrics=new VerticalStackLayout { Spacing=0,VerticalOptions=LayoutOptions.Center,Children={percent,time} };
        var head=new Grid { ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=8 };
        icon.VerticalOptions=LayoutOptions.Center;disconnect.VerticalOptions=LayoutOptions.Start;
        head.Add(icon);head.Add(new VerticalStackLayout { Spacing=3,VerticalOptions=LayoutOptions.Center,Children={name,transport} },1);head.Add(metrics,2);head.Add(disconnect,3);
        name.LineBreakMode=LineBreakMode.TailTruncation;name.MaxLines=1;
        var stack=Design.Stack(head,charge);stack.Spacing=6;Content=stack;
        disconnect.Clicked+=Disconnect;
        var pointer=new PointerGestureRecognizer();
        pointer.PointerEntered+=(_,_)=>{disconnect.BackgroundColor=Color.FromArgb(Design.IsDark?"#34485F":"#DFE9F5");disconnect.BorderColor=Color.FromArgb(Design.IsDark?"#7D95B2":"#7692AF");};
        pointer.PointerExited+=(_,_)=>{disconnect.BackgroundColor=Colors.Transparent;disconnect.BorderColor=Colors.Transparent;};
        disconnect.CornerRadius=6;disconnect.BorderWidth=1;disconnect.BorderColor=Colors.Transparent;disconnect.GestureRecognizers.Add(pointer);
    }
    public void Update(DeviceReading value)
    {
        // Draw a native font glyph without a bitmap or asynchronous image load.
        string key=value.Kind+Design.IsDark;
        if(iconKey!=key){icon.Text=Design.Glyph(value.Kind);icon.TextColor=Color.FromArgb(Design.IsDark?"#B3C7DC":"#526982");iconKey=key;}
        string unplug="unplug"+Design.IsDark;
        if(disconnectKey!=unplug){disconnect.TextColor=Color.FromArgb(Design.IsDark?"#95A3B8":"#526982");disconnectKey=unplug;}
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

