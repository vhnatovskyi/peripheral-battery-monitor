using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Text;
using System.Windows.Forms;
using PeripheralBatteryMonitor.Installation;

internal sealed class InstallationForm : Form
{
    internal readonly TextBox InstallFolder = new TextBox { Name="InstallFolder" };
    internal readonly Button Browse = new Button { Name="BrowseFolder" };
    internal readonly Button InstallButton = new Button { Name="InstallApplication" };
    private readonly Button cancel = new Button();
    private readonly Label status = new Label();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly bool ukrainian;
    private bool installing;
    private string T(string uk,string en) { return ukrainian ? uk : en; }
    internal InstallationForm()
    {
        ukrainian=ReadLanguage()=="uk";
        Text="Peripheral Battery Monitor — "+T("Встановлення","Setup");
        Font=new Font("Segoe UI",9);ClientSize=new Size(540,250);AutoScaleMode=AutoScaleMode.Dpi;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        using(var stream=typeof(InstallationForm).Assembly.GetManifestResourceStream("app-icon.ico"))
        using(var icon=new Icon(stream)) Icon=(Icon)icon.Clone();
        Controls.Add(new Label { Text=T("Встановлення програми","Install application"),Font=new Font("Segoe UI",14,FontStyle.Bold),AutoSize=true,Location=new Point(20,16) });
        Controls.Add(new Label { Text=T("Оберіть окрему папку для Peripheral Battery Monitor.","Choose a separate folder for Peripheral Battery Monitor."),AutoSize=true,Location=new Point(20,52) });
        Controls.Add(new Label { Text=T("Папка встановлення","Installation folder"),AutoSize=true,Location=new Point(20,88) });
        InstallFolder.SetBounds(20,110,394,27);InstallFolder.Text=InstallPaths.PreferredRoot();
        InstallFolder.AccessibleName=T("Папка встановлення","Installation folder");Controls.Add(InstallFolder);
        Browse.Text=T("Огляд…","Browse…");Browse.SetBounds(424,108,96,29);Browse.Click+=ChooseFolder;Controls.Add(Browse);
        status.Text=T("Оновлення використовуватимуть обрану папку.","Updates will use the selected folder.");status.SetBounds(20,148,500,34);Controls.Add(status);
        progress.SetBounds(20,187,500,5);progress.Style=ProgressBarStyle.Marquee;progress.Visible=false;Controls.Add(progress);
        InstallButton.Text=T("Встановити","Install");InstallButton.SetBounds(312,207,100,30);InstallButton.Click+=BeginInstall;Controls.Add(InstallButton);
        cancel.Text=T("Скасувати","Cancel");cancel.SetBounds(422,207,98,30);cancel.Click+=delegate { Close(); };Controls.Add(cancel);
        AcceptButton=InstallButton;CancelButton=cancel;FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(installing)e.Cancel=true; };
    }
    private void ChooseFolder(object sender,EventArgs e)
    {
        using(var dialog=new FolderBrowserDialog())
        {
            dialog.Description=T("Оберіть розташування папки програми","Choose where to create the application folder");dialog.ShowNewFolderButton=true;
            try { string parent=Path.GetDirectoryName(InstallFolder.Text);if(Directory.Exists(parent))dialog.SelectedPath=parent; } catch(ArgumentException) { }
            if(dialog.ShowDialog(this)==DialogResult.OK)
                InstallFolder.Text=InstallPaths.IsOwnedRoot(dialog.SelectedPath)?dialog.SelectedPath:Path.Combine(dialog.SelectedPath,"PeripheralBatteryMonitor");
        }
    }
    private async void BeginInstall(object sender,EventArgs e)
    {
        try
        {
            string destination=InstallPaths.ValidateDestination(InstallFolder.Text);InstallFolder.Text=destination;
            installing=true;InstallButton.Enabled=Browse.Enabled=InstallFolder.Enabled=cancel.Enabled=false;
            progress.Visible=true;status.Text=T("Встановлення… Зачекайте, будь ласка.","Installing… Please wait.");
            await Task.Run(delegate { Setup.Install(destination); });
            installing=false;
            MessageBox.Show(this,T("Програму встановлено у:\n","Application installed in:\n")+destination,Text,MessageBoxButtons.OK,MessageBoxIcon.Information);
            Process.Start(Path.Combine(destination,"PeripheralBatteryMonitor.exe"));Close();
        }
        catch(Exception error)
        {
            installing=false;InstallButton.Enabled=Browse.Enabled=InstallFolder.Enabled=cancel.Enabled=true;progress.Visible=false;
            status.Text=T("Не вдалося встановити програму. Перевірте обрану папку.","Installation failed. Check the selected folder.");
            MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
    }
    private static string ReadLanguage()
    {
        try
        {
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PeripheralBatteryMonitor","settings.json")))))
            {
                var value=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream);
                if(value.Language=="uk" || value.Language=="en")return value.Language;
            }
        }
        catch(IOException) { } catch(UnauthorizedAccessException) { } catch(SerializationException) { }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    }
    [DataContract] private sealed class Preferences { [DataMember] public string Language { get; set; } }
}
