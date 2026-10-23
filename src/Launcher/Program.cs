using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string current = Path.Combine(root, "current.txt");
            string package = root;
            if (File.Exists(current))
            {
                string version = File.ReadAllText(current).Trim();
                if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")) throw new IOException("Invalid installed version.");
                package = Path.Combine(root, "versions", version);
            }
            string script = Path.Combine(package, "src", "App", "Tray.ps1");
            if (!File.Exists(script)) throw new IOException("Application files are missing. Please reinstall Peripheral Battery Monitor.");
            using (var runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.ApartmentState = System.Threading.ApartmentState.STA;
                runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
                runspace.Open();
                Runspace.DefaultRunspace = runspace;
                using (var shell = PowerShell.Create())
                {
                    shell.Runspace = runspace;
                    shell.AddCommand(script);
                    shell.AddParameter("ShowWindow", Array.IndexOf(args, "--background") < 0);
                    shell.Invoke();
                    if (shell.HadErrors) throw new InvalidOperationException(shell.Streams.Error[0].ToString());
                }
            }
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Peripheral Battery Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
