using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;

namespace CyberpunkModManager.ViewModels
{
    public class FilesViewModel
    {
        public ObservableCollection<InstalledModDisplay> DownloadedFiles { get; set; } = new();

        public FilesViewModel()
        {
            LoadDownloadedFiles();
        }

        private void LoadDownloadedFiles()
        {
            string path = Path.Combine(Settings.DefaultModsDir, "installed_mods.json");
            if (!File.Exists(path)) return;

            try
            {
                string json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();

                foreach (var entry in list)
                {
                    double sizeMB = 0;
                    string folder = Path.Combine(Settings.DefaultModsDir, PathUtils.SanitizeModName(entry.ModName));
                    string fullPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(entry.FileName) + ".zip");

                    if (File.Exists(fullPath))
                    {
                        long sizeBytes = new FileInfo(fullPath).Length;
                        sizeMB = sizeBytes / 1024.0 / 1024.0;
                    }

                    DownloadedFiles.Add(new InstalledModDisplay
                    {
                        ModName = entry.ModName,
                        FileName = entry.FileName,
                        UploadedTimestamp = entry.UploadedTimestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        FileSizeDisplay = sizeMB > 1 ? $"{sizeMB:F2} MB" : $"{sizeMB * 1024:F2} KB",
                        Status = "Installed"
                    });
                }
            }
            catch
            {
                // Handle or log error
            }
        }
    }

    public class InstalledModDisplay
    {
        public string ModName { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileSizeDisplay { get; set; } = "";
        public string UploadedTimestamp { get; set; } = "";
        public string Status { get; set; } = "Installed";
    }
}
