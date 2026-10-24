using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using PeripheralBatteryMonitor.Installation;

internal static class InstallerFormTests
{
    [STAThread] private static int Main(string[] args)
    {
        string temporary=Path.Combine(Path.GetTempPath(),"PbmSetupTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        try
        {
            string custom=Path.Combine(temporary,"Custom apps","Заряд пристроїв");
            if(InstallPaths.ValidateDestination(custom)!=custom)throw new Exception("Custom path rejected");
            Reject(delegate { InstallPaths.ValidateDestination(Path.GetPathRoot(temporary)); });
            Reject(delegate { InstallPaths.ValidateDestination("relative-folder"); });
            string unrelated=Path.Combine(temporary,"Documents");Directory.CreateDirectory(unrelated);File.WriteAllText(Path.Combine(unrelated,"keep.txt"),"Keep this file");
            Reject(delegate { InstallPaths.ValidateDestination(unrelated); });
            Directory.CreateDirectory(Path.Combine(custom,"versions","1.2.3","installer"));
            File.WriteAllText(Path.Combine(custom,"current.txt"),"1.2.3");File.WriteAllText(Path.Combine(custom,"PeripheralBatteryMonitor.exe"),"fixture");
            File.WriteAllText(Path.Combine(custom,"versions","1.2.3","version.json"),"{\"repository\":\"vhnatovskyi/peripheral-battery-monitor\",\"version\":\"1.2.3\"}");
            string script=Path.Combine(custom,"versions","1.2.3","installer","Uninstall.ps1");File.WriteAllText(script,"#fixture");
            if(!InstallPaths.IsOwnedRoot(custom) || InstallPaths.ValidateDestination(custom)!=custom)throw new Exception("Existing installation not recognized");
            if(InstallPaths.ValidateUninstallTarget(custom,script,custom)!=custom)throw new Exception("Custom uninstall target rejected");
            Reject(delegate { InstallPaths.ValidateUninstallTarget(custom,script,unrelated); });
            Reject(delegate { InstallPaths.ValidateUninstallTarget(custom,Path.Combine(temporary,"Uninstall.ps1"),custom); });
            Reject(delegate { InstallPaths.ValidateDestination(Path.Combine(custom,"Nested copy")); });
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new InstallationForm())
            {
                if(form.InstallFolder.Text!=InstallPaths.PreferredRoot() || !form.Browse.Enabled || !form.InstallButton.Enabled)throw new Exception("Folder chooser UI is incomplete");
                form.InstallFolder.Text=@"D:\Apps\PeripheralBatteryMonitor";
                form.Show();Application.DoEvents();
                form.Browse.Focus();form.InstallFolder.SelectionLength=0;
                using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));if(args.Length>0)image.Save(Path.GetFullPath(args[0]),ImageFormat.Png); }
                form.InstallFolder.Text=Path.Combine(temporary,"Cancelled install");
                ((IButtonControl)form.CancelButton).PerformClick();
                if(Directory.Exists(form.InstallFolder.Text))throw new Exception("Cancel created installation files");
            }
            if(File.ReadAllText(Path.Combine(unrelated,"keep.txt"))!="Keep this file")throw new Exception("Unrelated data changed");
            Console.WriteLine("Installer folder validation, ownership, uninstall guards and cancellation tests passed");return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error);return 1; }
        finally
        {
            string prefix=Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\')+"\\PbmSetupTests-";
            if(Path.GetFullPath(temporary).StartsWith(prefix,StringComparison.OrdinalIgnoreCase))Directory.Delete(temporary,true);
        }
    }
    private static void Reject(Action action) { try { action(); } catch(IOException) { return; }throw new Exception("Unsafe installation path accepted"); }
}
