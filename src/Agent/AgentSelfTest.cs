using System.IO.Pipes;
using System.Text.Json;
using PeripheralBatteryMonitor.Desktop.Services;
using PeripheralBatteryMonitor.Desktop.Platforms.Windows;
using PeripheralBatteryMonitor.Interop;

namespace PeripheralBatteryMonitor.Agent;

// The isolated test uses the real message loop, pipe framing, settings store
// and layered widget, without touching hardware, startup registration or tray.
internal static class AgentSelfTest
{
    internal static int ExitCode=1;
    internal static async Task RunAsync(AgentHost host)
    {
        string result=Path.Combine(SettingsStore.StateRoot,"agent-result.txt");
        try
        {
            var first=await host.RequestForTest(new());
            Assert(first.Success && first.Snapshot?.Devices.Length==3,"Initial snapshot");
            Assert(first.Snapshot!.Devices[0].Percent==90,"Battery percentage");
            var unchanged=await host.RequestForTest(new(){Instance=first.Snapshot.Instance,Sequence=first.Snapshot.Sequence});
            Assert(unchanged.Success && unchanged.Snapshot==null,"Unchanged snapshot must not retransmit the device list");
            var settings=first.Snapshot.Settings.Copy();
            settings.WidgetEnabled=true;settings.WidgetMode="Minimized";settings.WidgetOrientation="Horizontal";settings.WidgetOpacity=0;
            settings.WidgetHiddenDevices=["test-mouse"];settings.TrayHiddenDevices=["test-keyboard"];
            var saved=await host.RequestForTest(new(){Command="settings",Settings=settings});
            Assert(saved.Success,"Settings acknowledgement");
            await OnThread(host,()=>
            {
                var widget=host.Widget!;
                Assert(widget.DeviceCount==2 && widget.ClickThrough && widget.IsAlwaysOnTop && !widget.HasBorder,"Passive widget visibility");
                Assert(widget.BackgroundOpacity==0,"Fully transparent background");
                Assert(TrayHost.VisibleDevices(first.Snapshot.Devices,host.Preferences).Length==2,"Independent tray visibility");
                using var image=widget.RenderPreview();
                int opaque=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A==255)opaque++;
                Assert(opaque>20 && image.GetPixel(0,0).A==0,"Opaque content on transparent background");
                widget.SetMoving(true);widget.CheckInteractionFocus(false);Assert(widget.ClickThrough && !widget.IsMoving,"Focus drag guard");
            });
            await Task.Delay(450);
            var disk=new SettingsStore().Value;
            Assert(!disk.ShowInWidget("test-mouse") && !disk.ShowInTray("test-keyboard") && disk.WidgetOrientation=="Horizontal","Persisted settings");
            settings.WidgetOrientation="Vertical";settings.Language="en";settings.Theme="Light";
            Assert((await host.RequestForTest(new(){Command="settings",Settings=settings})).Success,"Live layout and language");
            await OnThread(host,()=>Assert(host.Widget!.Bounds.Height>host.Widget.Bounds.Width/2,"Vertical layout"));
            var invalid=settings.Copy();invalid.WidgetMode="invalid";
            Assert(!(await host.RequestForTest(new(){Command="settings",Settings=invalid})).Success,"Reject invalid settings");
            Assert(!(await host.RequestForTest(new(){Command="snapshot",WindowHandle=1,ProcessId=Environment.ProcessId})).Success,"Reject foreign UI window");
            // A malformed connection must not stop the next legitimate client.
            using(var pipe=new NamedPipeClientStream(".",host.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly))
            {await pipe.ConnectAsync(2000);await pipe.WriteAsync(BitConverter.GetBytes(1024*1024));}
            Assert((await host.RequestForTest(new())).Success,"Recover after malformed frame");
            settings.WidgetEnabled=false;
            await host.RequestForTest(new(){Command="settings",Settings=settings});
            await OnThread(host,()=>Assert(host.Widget==null,"Disable widget"));
            await Task.Delay(450);
            if(Environment.GetEnvironmentVariable("PBM_AGENT_TEST_UI")=="1")
            {
                var start=new System.Diagnostics.ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"PeripheralBatteryMonitor.Desktop.exe")){UseShellExecute=false,CreateNoWindow=true};
                start.ArgumentList.Add("--agent-client");start.ArgumentList.Add("--agent-integration-test");
                start.Environment["PBM_AGENT_PIPE_NAME"]=host.PipeName;start.Environment["PBM_AGENT_TEST_ID"]=Guid.NewGuid().ToString("N");
                using var ui=System.Diagnostics.Process.Start(start)!;
                try{await ui.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));}catch{if(!ui.HasExited)ui.Kill();throw;}
                string uiResult=File.ReadAllText(Path.Combine(SettingsStore.StateRoot,"agent-ui-result.txt"));
                Assert(ui.ExitCode==0 && uiResult.StartsWith("PASS:"),uiResult);
                await Task.Delay(450);
                var final=await host.RequestForTest(new());
                Assert(final.Snapshot!.Settings.WidgetOpacity==37 && new SettingsStore().Value.WidgetOpacity==37,"Flush final setting on UI close");
                Assert(final.Snapshot.Devices.Length==3,"Agent remains active after UI close");
            }
            Directory.CreateDirectory(SettingsStore.StateRoot);
            await File.WriteAllTextAsync(result,"PASS: real agent IPC, live settings, disk persistence, transparent widget, layouts, focus guard, independent visibility and malformed-client recovery");
            ExitCode=0;
        }
        catch(Exception error){Directory.CreateDirectory(SettingsStore.StateRoot);await File.WriteAllTextAsync(result,error.ToString());}
        finally{host.Post(host.Quit);}
    }
    private static void Assert(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static Task OnThread(AgentHost host,Action action)
    {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Post(()=>{try{action();completion.SetResult();}catch(Exception error){completion.SetException(error);}});return completion.Task;
    }
}
