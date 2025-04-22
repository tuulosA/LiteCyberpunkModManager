using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using LiteCyberpunkModManager.Helpers;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;

namespace LiteCyberpunkModManager.ViewModels
{
    public class FilesViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<InstalledModDisplay> AllDownloadedFiles { get; set; } = new();
        public ObservableCollection<InstalledModDisplay> FilteredDownloadedFiles { get; set; } = new();

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilter();
            }
        }

        public FilesViewModel()
        {
            LoadDownloadedFiles();
        }

        public void Reload()
        {
            AllDownloadedFiles.Clear();
            FilteredDownloadedFiles.Clear();
            LoadDownloadedFiles();
        }

        private void LoadDownloadedFiles()
        {
            string metadataPath = PathConfig.DownloadedMods;
            string installTrackingPath = PathConfig.InstalledGameFiles;

            if (!File.Exists(metadataPath)) return;

            try
            {
                string json = File.ReadAllText(metadataPath);
                var list = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();
                HashSet<string> installedGameFiles = new();

                if (File.Exists(installTrackingPath))
                {
                    string gameJson = File.ReadAllText(installTrackingPath);
                    var installedList = JsonSerializer.Deserialize<List<InstalledGameFile>>(gameJson) ?? new();

                    foreach (var entry in installedList)
                    {
                        installedGameFiles.Add(Path.GetFileNameWithoutExtension(entry.FileName).ToLower() + ".archive");
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

                    string cleanName = Path.GetFileNameWithoutExtension(entry.FileName).ToLower();
                    bool isInstalled = installedGameFiles.Contains(cleanName + ".archive");

                    var display = new InstalledModDisplay
                    {
                        ModName = entry.ModName,
                        FileName = entry.FileName,
                        FileSizeMB = sizeMB,
                        UploadedTimestamp = entry.UploadedTimestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        Status = isInstalled ? "Installed" : "Not Installed"
                    };

                    AllDownloadedFiles.Add(display);
                }

                ApplyFilter();
            }
            catch { }
        }

        private void ApplyFilter()
        {
            FilteredDownloadedFiles.Clear();
            foreach (var mod in AllDownloadedFiles)
            {
                if (string.IsNullOrWhiteSpace(_searchText) ||
                    mod.ModName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    mod.FileName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                {
                    FilteredDownloadedFiles.Add(mod);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }


    public class InstalledModDisplay
    {
        public string ModName { get; set; } = "";
        public string FileName { get; set; } = "";
        public double FileSizeMB { get; set; }
        public string FileSizeDisplay => FileSizeMB > 1
            ? $"{FileSizeMB:F2} MB"
            : $"{FileSizeMB * 1024:F2} KB";

        public string UploadedTimestamp { get; set; } = "";
        public string Status { get; set; } = "Not Installed";
    }

}
