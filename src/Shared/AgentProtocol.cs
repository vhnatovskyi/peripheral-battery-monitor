using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using PeripheralBatteryMonitor.Desktop.Services;

namespace PeripheralBatteryMonitor.Interop;

internal sealed class AgentSnapshot
{
    public string Instance { get; set; }="";
    public long Sequence { get; set; }
    public long NavigationSequence { get; set; }
    public string Route { get; set; }="devices";
    public MonitorSettings Settings { get; set; }=new();
    public DeviceReading[] Devices { get; set; }=[];
    public string? SaveError { get; set; }
}
internal sealed class AgentRequest
{
    public string Command { get; set; }="snapshot";
    public MonitorSettings? Settings { get; set; }
    public long WindowHandle { get; set; }
    public int ProcessId { get; set; }
    public string Instance { get; set; }="";
    public long Sequence { get; set; }=-1;
}
internal sealed class AgentResponse
{
    public bool Success { get; set; }=true;
    public string? Error { get; set; }
    public AgentSnapshot? Snapshot { get; set; }
}
[JsonSerializable(typeof(AgentSnapshot))]
[JsonSerializable(typeof(AgentRequest))]
[JsonSerializable(typeof(AgentResponse))]
internal partial class AgentJsonContext:JsonSerializerContext { }

internal static class AgentWire
{
    internal static string PipeName { get; }="PeripheralBatteryMonitor.Agent.v1."+System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value+"."+System.Diagnostics.Process.GetCurrentProcess().SessionId;
    private const int MaximumBytes=512*1024;
    internal static async Task WriteAsync<T>(Stream stream,T value,System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,CancellationToken stop)
    {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,type);
        if(bytes.Length>MaximumBytes)throw new IOException("Agent message is too large");
        byte[] length=BitConverter.GetBytes(bytes.Length);
        await stream.WriteAsync(length,stop);await stream.WriteAsync(bytes,stop);await stream.FlushAsync(stop);
    }
    internal static async Task<T> ReadAsync<T>(Stream stream,System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type,CancellationToken stop)
    {
        byte[] header=new byte[4];await stream.ReadExactlyAsync(header,stop);
        int length=BitConverter.ToInt32(header);
        if(length<1 || length>MaximumBytes)throw new IOException("Invalid agent message length");
        byte[] bytes=new byte[length];await stream.ReadExactlyAsync(bytes,stop);
        return JsonSerializer.Deserialize(bytes,type)??throw new IOException("Empty agent message");
    }
    internal static async Task<AgentResponse> SendAsync(AgentRequest request,CancellationToken stop,string? pipeName=null)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop);timeout.CancelAfter(TimeSpan.FromSeconds(3));
        string? testPipe=Environment.GetCommandLineArgs().Contains("--agent-integration-test")?Environment.GetEnvironmentVariable("PBM_AGENT_PIPE_NAME"):null;
        using var pipe=new NamedPipeClientStream(".",pipeName??testPipe??PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);
        await WriteAsync(pipe,request,AgentJsonContext.Default.AgentRequest,timeout.Token);
        return await ReadAsync(pipe,AgentJsonContext.Default.AgentResponse,timeout.Token);
    }
}
