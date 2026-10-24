using PeripheralBatteryMonitor.Desktop.Services;
namespace PeripheralBatteryMonitor.Desktop.Platforms.Windows;

// Native desktop composition tests use the same widget as the background host.
internal sealed class BatteryWidget : NativeBatteryWidget
{
    private readonly Microsoft.UI.Xaml.DispatcherTimer guard=new() { Interval=TimeSpan.FromMilliseconds(100) };
    public BatteryWidget(Microsoft.UI.Xaml.Window main,bool persistPosition=true)
        : base(new WidgetContext(()=>AppRuntime.Current.Settings.Value,()=>Views.Design.IsDark,()=>WinRT.Interop.WindowNative.GetWindowHandle(main),SettingsStore.StateRoot),persistPosition)
    { guard.Tick+=(_,_)=>Tick();guard.Start(); }
    public override void Dispose(){guard.Stop();base.Dispose();}
}