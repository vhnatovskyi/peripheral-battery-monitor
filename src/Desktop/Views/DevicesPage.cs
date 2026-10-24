using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class DevicesPage : ContentPage
{
    private readonly Grid cards=new() { ColumnSpacing=12,RowSpacing=12 };
    private readonly Label summary=Design.Hint(""),empty=Design.Label("",20),pageLabel=Design.Hint("");
    private readonly HorizontalStackLayout paging=new() { Spacing=12,HorizontalOptions=LayoutOptions.Center };
    private readonly Dictionary<string,DeviceCardView> views=[];
    private int page;
    private string layoutKey="";
    public DevicesPage()
    {
        var content=new Grid { Padding=16,RowDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],RowSpacing=12 };
        content.Add(summary);content.Add(cards,0,1);content.Add(paging,0,2);
        paging.Add(Design.Button("‹",(_,_)=>{page=Math.Max(0,page-1);Refresh();}));paging.Add(pageLabel);
        paging.Add(Design.Button("›",(_,_)=>{page++;Refresh();}));
        Content=content;
        var refresh=new ImageButton { AutomationId="RefreshDevices",WidthRequest=32,HeightRequest=32,Padding=7,CornerRadius=6,BackgroundColor=Colors.Transparent };
        refresh.Clicked+=(_,_)=>AppRuntime.Current.Refresh();
        var hover=new PointerGestureRecognizer();hover.PointerEntered+=(_,_)=>refresh.BackgroundColor=Color.FromArgb(Design.IsDark?"#34485F":"#DFE9F5");hover.PointerExited+=(_,_)=>refresh.BackgroundColor=Colors.Transparent;refresh.GestureRecognizers.Add(hover);
        var heading=Design.Label("",18,true);heading.VerticalOptions=LayoutOptions.Center;
        var toolbar=new Grid { ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(12,0),HeightRequest=36 };
        toolbar.Add(heading);toolbar.Add(refresh,1);Shell.SetTitleView(this,toolbar);
        void Translate(){heading.Text=Design.T("Пристрої","Devices");string caption=Design.T("Оновити заряд пристроїв","Refresh device batteries");refresh.Source=Design.IsDark?"refresh.png":"refresh_light.png";ToolTipProperties.SetText(refresh,caption);SemanticProperties.SetDescription(refresh,caption);layoutKey="";Refresh();}
        AppRuntime.Current.Changed+=Refresh;AppRuntime.Current.SettingsChanged+=Translate;AppRuntime.Current.AppearanceChanged+=Translate;
        SizeChanged+=(_,_)=>{toolbar.WidthRequest=Math.Max(120,Width-8);Refresh();};Translate();
    }
    private void Refresh()
    {
        var readings=AppRuntime.Current.Readings;
        summary.Text=readings.Count==0?Design.T("Підключіть пристрій — він зʼявиться автоматично.","Connect a device — it will appear automatically."):
            Design.T($"Підключено: {readings.Count}",$"Connected: {readings.Count}");
        int columns=Width>=780?3:Width>=500?2:1;
        int rows=Math.Clamp((int)((Height-88)/140),1,3),capacity=columns*rows;
        int pages=Math.Max(1,(readings.Count+capacity-1)/capacity);page=Math.Clamp(page,0,pages-1);
        paging.IsVisible=pages>1;pageLabel.Text=$"{page+1} / {pages}";
        var selected=readings.Skip(page*capacity).Take(capacity).ToArray();
        string key=columns+":"+rows+":"+page+":"+string.Join("|",selected.Select(d=>d.Id));
        if(key!=layoutKey)
        {
            cards.Clear();cards.RowDefinitions.Clear();cards.ColumnDefinitions.Clear();
            for(int col=0;col<columns;col++)cards.ColumnDefinitions.Add(new(GridLength.Star));
            for(int row=0;row<rows;row++)cards.RowDefinitions.Add(new(GridLength.Auto));
            for(int i=0;i<selected.Length;i++)
            {if(!views.TryGetValue(selected[i].Id,out var view)){view=new();views[selected[i].Id]=view;}cards.Add(view,i%columns,i/columns);}
            foreach(string id in views.Keys.Except(readings.Select(d=>d.Id)).ToArray())views.Remove(id);
            if(readings.Count==0){empty.Text=Design.T("Очікуємо на пристрої","Waiting for devices");cards.Add(empty);Grid.SetColumnSpan(empty,columns);}
            layoutKey=key;
        }
        foreach(var item in selected)views[item.Id].Update(item);
    }
}

