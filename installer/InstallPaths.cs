using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Text;
using Microsoft.Win32;

namespace PeripheralBatteryMonitor.Installation
{
    public static class InstallPaths
    {
        public const string Registration = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\PeripheralBatteryMonitor";
        public static string DefaultRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PeripheralBatteryMonitor"); } }
        public static string PreferredRoot()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(Registration))
            {
                string path = key == null ? null : key.GetValue("InstallLocation") as string;
                if (!String.IsNullOrWhiteSpace(path) && IsOwnedRoot(path)) return Normalize(path);
            }
            return DefaultRoot;
        }
        public static string Normalize(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !Regex.IsMatch(path, @"^[A-Za-z]:[\\/]"))
                throw new IOException("Choose an absolute folder on a local drive.");
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length <= 3) throw new IOException("Choose a separate application folder, not the drive root.");
            return full;
        }
        public static bool IsOwnedRoot(string path)
        {
            try
            {
                path = Normalize(path);
                string pointer = Path.Combine(path, "current.txt");
                if (!File.Exists(pointer) || !File.Exists(Path.Combine(path, "PeripheralBatteryMonitor.exe"))) return false;
                string version = File.ReadAllText(pointer).Trim();
                if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")) return false;
                string metadata = Path.Combine(path, "versions", version, "version.json");
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(metadata))))
                    return ((PackageIdentity)new DataContractJsonSerializer(typeof(PackageIdentity)).ReadObject(stream)).Repository == "vhnatovskyi/peripheral-battery-monitor";
            }
            catch (Exception error)
            {
                if (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is SerializationException) return false;
                throw;
            }
        }
        public static string ValidateDestination(string path)
        {
            path = Normalize(path);
            if (File.Exists(path)) throw new IOException("The destination is a file. Choose an application folder.");
            for (var parent = new DirectoryInfo(path); parent != null; parent = parent.Parent)
            {
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Choose a local folder without directory links.");
                if (!String.Equals(parent.FullName, path, StringComparison.OrdinalIgnoreCase) && IsOwnedRoot(parent.FullName))
                    throw new IOException("Do not install inside another Peripheral Battery Monitor installation.");
            }
            if (Directory.Exists(path))
            {
                bool empty = true;
                using (var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator()) empty = !entries.MoveNext();
                if (!empty && !IsOwnedRoot(path)) throw new IOException("Choose an empty folder or an existing Peripheral Battery Monitor installation.");
                RejectLinks(path);
            }
            return path;
        }
        public static string ValidateUninstall(string path, string script)
        { return ValidateUninstallTarget(path, script, PreferredRoot()); }
        internal static string ValidateUninstallTarget(string path, string script, string registeredRoot)
        {
            path = ValidateDestination(path);
            if (!IsOwnedRoot(path) || !String.Equals(path, Normalize(registeredRoot), StringComparison.OrdinalIgnoreCase))
                throw new IOException("The folder does not match the registered application installation.");
            string fullScript = Path.GetFullPath(script);
            if (!fullScript.StartsWith(path + @"\versions\", StringComparison.OrdinalIgnoreCase) || !fullScript.EndsWith(@"\installer\Uninstall.ps1", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Run the uninstaller from the registered application installation.");
            return path;
        }
        private static void RejectLinks(string path)
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(path))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("The installation folder contains a file or directory link.");
                if ((attributes & FileAttributes.Directory) != 0) RejectLinks(entry);
            }
        }
        [DataContract] private sealed class PackageIdentity
        { [DataMember(Name="repository")] public string Repository { get; set; } }
    }
}
