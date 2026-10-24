using Microsoft.Maui.Controls.Shapes;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Desktop.Views;

internal sealed class DevicesPage : ContentPage
{
    private readonly Grid cards=new() { ColumnSpacing=16,RowSpacing=16 };
    private readonly Label summary=Design.Hint(""),empty=Design.Label("",20),pageLabel=Design.Hint("");
    private readonly HorizontalStackLayout paging=new() { Spacing=12,HorizontalOptions=LayoutOptions.Center };
    private readonly Dictionary<string,DeviceCardView> views=[];
    private int page;
    private string layoutKey="";
    public DevicesPage()
    {
        var content=new Grid { Padding=24,RowDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],RowSpacing=18 };
        content.Add(summary);content.Add(cards,0,1);content.Add(paging,0,2);
        paging.Add(Design.Button("‹",(_,_)=>{page=Math.Max(0,page-1);Refresh();}));paging.Add(pageLabel);
        paging.Add(Design.Button("›",(_,_)=>{page++;Refresh();}));
        Content=content;
        var refresh=new ToolbarItem();refresh.Clicked+=(_,_)=>AppRuntime.Current.Refresh();ToolbarItems.Add(refresh);
        void Translate(){refresh.Text=Design.T("Оновити","Refresh");layoutKey="";Refresh();}
        AppRuntime.Current.Changed+=Refresh;AppRuntime.Current.SettingsChanged+=Translate;
        SizeChanged+=(_,_)=>Refresh();Translate();
    }
    private void Refresh()
    {
        var readings=AppRuntime.Current.Readings;
        summary.Text=readings.Count==0?Design.T("Підключіть пристрій — він зʼявиться автоматично.","Connect a device — it will appear automatically."):
            Design.T($"Підключено: {readings.Count}",$"Connected: {readings.Count}");
        int columns=Width>=780?3:Width>=500?2:1;
        int rows=Math.Clamp((int)((Height-140)/192),1,3),capacity=columns*rows;
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

