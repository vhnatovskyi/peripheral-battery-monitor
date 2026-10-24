using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

internal static class Setup
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallationForm());
    }
    internal static void Install(string destination)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "PeripheralBatteryMonitor-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temporary);
            string archive = Path.Combine(temporary, "payload.zip");
            using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            using (var output = File.Create(archive)) payload.CopyTo(output);
            string package = Path.Combine(temporary, "package");
            ZipFile.ExtractToDirectory(archive, package);
            var start = new ProcessStartInfo("powershell.exe", "-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + Path.Combine(package, "installer", "Install.ps1") + "\" -PackageRoot \"" + package + "\" -InstallRoot \"" + destination + "\" -NoLaunch");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardError = true;
            using (var process = Process.Start(start))
            {
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new IOException(error.Length > 0 ? error : "Installation failed.");
            }
        }
        finally
        {
            try
            {
                string prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\')+"\\PeripheralBatteryMonitor-";
                if (Path.GetFullPath(temporary).StartsWith(prefix,StringComparison.OrdinalIgnoreCase) && Directory.Exists(temporary)) Directory.Delete(temporary,true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
