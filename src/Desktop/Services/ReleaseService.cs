using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PeripheralBatteryMonitor.Desktop.Services;

public sealed record ReleaseAsset([property:JsonPropertyName("name")]string Name,[property:JsonPropertyName("browser_download_url")]string Url,[property:JsonPropertyName("size")]long Size);
public sealed record ReleaseInfo([property:JsonPropertyName("tag_name")]string Tag,[property:JsonPropertyName("name")]string Name,
    [property:JsonPropertyName("body")]string? Notes,[property:JsonPropertyName("published_at")]DateTimeOffset Published,
    [property:JsonPropertyName("assets")]ReleaseAsset[] Assets,[property:JsonPropertyName("draft")]bool Draft,[property:JsonPropertyName("prerelease")]bool Prerelease)
{
    public Version Version=>System.Version.Parse(Tag.TrimStart('v'));
}
public sealed class ReleaseService
{
    public const string Repository="vhnatovskyi/peripheral-battery-monitor";
    private static readonly HttpClient http=CreateClient();
    private static HttpClient CreateClient()
    {
        var client=new HttpClient { Timeout=TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PeripheralBatteryMonitor","0.5.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));return client;
    }
    public async Task<IReadOnlyList<ReleaseInfo>> LoadAsync(CancellationToken token=default)
    {
        using var response=await http.GetAsync($"https://api.github.com/repos/{Repository}/releases?per_page=100",token);response.EnsureSuccessStatusCode();
        var releases=await JsonSerializer.DeserializeAsync(await response.Content.ReadAsStreamAsync(token),AppJsonContext.Default.ReleaseInfoArray,token) ?? [];
        return releases.Where(r=>!r.Draft && !r.Prerelease && Regex.IsMatch(r.Tag,@"^v?\d+\.\d+\.\d+$") &&
            r.Assets.Any(a=>a.Name==$"PeripheralBatteryMonitor-{r.Version}-win-x64.zip") && r.Assets.Any(a=>a.Name=="SHA256SUMS.txt"))
            .OrderByDescending(r=>r.Version).ToArray();
    }
    public async Task InstallAsync(ReleaseInfo release,IProgress<double> progress,CancellationToken token=default)
    {
        string? installedRoot=SettingsStore.InstalledRoot;
        string defaultRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","PeripheralBatteryMonitor");
        if(installedRoot!=null && !Path.GetFullPath(installedRoot).Equals(defaultRoot,StringComparison.OrdinalIgnoreCase) && release.Version<new Version(0,5,8))
            throw new InvalidOperationException(AppRuntime.Current.Text("Релізи до 0.5.8 не підтримують встановлення у вибрану папку. Оберіть версію 0.5.8 або новішу.","Releases before 0.5.8 do not support custom installation folders. Choose version 0.5.8 or newer."));
        string portable=$"PeripheralBatteryMonitor-{release.Version}-portable-win-x64.zip";
        string name=release.Assets.Any(a=>a.Name==portable) ? portable : $"PeripheralBatteryMonitor-{release.Version}-win-x64.zip";
        var asset=release.Assets.Single(a=>a.Name==name);var sums=release.Assets.Single(a=>a.Name=="SHA256SUMS.txt");
        string allowed=$"https://github.com/{Repository}/releases/download/{release.Tag}/";
        foreach(var item in new[]{asset,sums})if(!item.Url.StartsWith(allowed,StringComparison.Ordinal))throw new InvalidDataException("Unexpected release URL.");
        if(asset.Size>400L*1024*1024 || sums.Size>65536)throw new InvalidDataException("Release asset is too large.");
        string folder=Path.Combine(SettingsStore.StateRoot,"Updates",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string zip=Path.Combine(folder,name);
        using(var response=await http.GetAsync(asset.Url,HttpCompletionOption.ResponseHeadersRead,token))
        {
            response.EnsureSuccessStatusCode();
            await using var input=await response.Content.ReadAsStreamAsync(token);
            await using var output=File.Create(zip);
            byte[] buffer=new byte[81920];long total=0;int count;
            while((count=await input.ReadAsync(buffer,token))>0)
            {
                total+=count;if(total>400L*1024*1024)throw new InvalidDataException("Release exceeds download limit.");
                await output.WriteAsync(buffer.AsMemory(0,count),token);if(asset.Size>0)progress.Report(Math.Min(.9,total/(double)asset.Size*.9));
            }
        }
        string manifest=await http.GetStringAsync(sums.Url,token);
        if(manifest.Length>65536)throw new InvalidDataException("Invalid checksum manifest.");
        var match=Regex.Match(manifest,@"(?m)^([a-fA-F0-9]{64})\s+\*?"+Regex.Escape(name)+@"\s*$");
        if(!match.Success)throw new InvalidDataException("Release checksum is missing.");
        await using(var stream=File.OpenRead(zip))
        { if(!Convert.ToHexString(await SHA256.HashDataAsync(stream,token)).Equals(match.Groups[1].Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Release checksum mismatch."); }
        string extracted=Path.Combine(folder,"package");ValidateArchive(zip,extracted);ZipFile.ExtractToDirectory(zip,extracted);
        using var metadata=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(extracted,"version.json"),token));
        if(metadata.RootElement.GetProperty("version").GetString()!=release.Version.ToString() || metadata.RootElement.GetProperty("repository").GetString()!=Repository)
            throw new InvalidDataException("Package metadata differs from release.");
        string installer=Path.Combine(extracted,"installer","Install.ps1");if(!File.Exists(installer))throw new InvalidDataException("Installer is missing.");
        progress.Report(1);
        _=Process.Start(CreateInstallerStart(installer,extracted,installedRoot)) ?? throw new IOException("Could not start installer.");
    }
    internal static ProcessStartInfo CreateInstallerStart(string installer,string extracted,string? installedRoot)
    {
        var start=new ProcessStartInfo("powershell.exe") { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=extracted };
        foreach(string argument in new[]{"-NoProfile","-STA","-ExecutionPolicy","Bypass","-File",installer,"-PackageRoot",extracted})start.ArgumentList.Add(argument);
        if(installedRoot!=null){start.ArgumentList.Add("-InstallRoot");start.ArgumentList.Add(installedRoot);}
        string modules=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","Modules");
        start.Environment["PSModulePath"]=modules+Path.PathSeparator+Environment.GetEnvironmentVariable("PSModulePath");
        return start;
    }
    public static void ValidateArchive(string path,string destination)
    {
        string root=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        using var archive=ZipFile.OpenRead(path);long length=0;
        if(archive.Entries.Count>10000)throw new InvalidDataException("Too many archive entries.");
        foreach(var entry in archive.Entries)
        {
            string target=Path.GetFullPath(Path.Combine(destination,entry.FullName));
            if(entry.FullName.Contains(':') || !target.StartsWith(root,StringComparison.OrdinalIgnoreCase) || ((entry.ExternalAttributes>>16)&0xF000)==0xA000)
                throw new InvalidDataException("Unsafe archive path.");
            length+=entry.Length;if(length>1024L*1024*1024)throw new InvalidDataException("Release archive is too large.");
        }
    }
}
