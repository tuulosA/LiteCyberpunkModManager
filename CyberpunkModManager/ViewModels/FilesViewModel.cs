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

        public void Reload()
        {
            DownloadedFiles.Clear();
            LoadDownloadedFiles();
        }

        private void LoadDownloadedFiles()
        {
            string metadataPath = Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");
            string installTrackingPath = Path.Combine(Settings.DefaultModsDir, "installed_game_files.json");

            if (!File.Exists(metadataPath)) return;

            try
            {
                string json = File.ReadAllText(metadataPath);
                var list = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();

                HashSet<string> installedGameFiles = new();

                if (File.Exists(installTrackingPath))
                {
                    string gameJson = File.ReadAllText(installTrackingPath);
                    var installedList = JsonSerializer.Deserialize<List<InstalledModInfo>>(gameJson) ?? new();

                    foreach (var entry in installedList)
                    {
                        installedGameFiles.Add(entry.FileName.ToLower());
                    }
                }

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

                    // ✅ Determine install status based on installed_game_files.json
                    string cleanName = Path.GetFileNameWithoutExtension(entry.FileName).ToLower();
                    bool isInstalled = installedGameFiles.Contains(cleanName + ".archive");

                    DownloadedFiles.Add(new InstalledModDisplay
                    {
                        ModName = entry.ModName,
                        FileName = entry.FileName,
                        FileSizeMB = sizeMB, // ✅ use actual value
                        UploadedTimestamp = entry.UploadedTimestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        Status = isInstalled ? "Installed" : "Not Installed"
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
        public double FileSizeMB { get; set; } // ✅ For proper sorting
        public string FileSizeDisplay => FileSizeMB > 1
            ? $"{FileSizeMB:F2} MB"
            : $"{FileSizeMB * 1024:F2} KB"; // Kept for formatted display

        public string UploadedTimestamp { get; set; } = "";
        public string Status { get; set; } = "Not Installed";
    }

}
