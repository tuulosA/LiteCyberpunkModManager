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
        public ObservableCollection<InstalledModDisplay> AllDownloadedFiles { get; } = new();
        public ObservableCollection<InstalledModDisplay> FilteredDownloadedFiles { get; } = new();

        public int TotalCount => FilteredDownloadedFiles.Count;
        public int InstalledCount => FilteredDownloadedFiles.Count(x => x.Status.StartsWith("Installed", System.StringComparison.OrdinalIgnoreCase));
        public int NotInstalledCount => TotalCount - InstalledCount;
        public string SummaryText => $"{TotalCount} files — {InstalledCount} installed, {NotInstalledCount} not installed";

        public void RefreshSummary()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(InstalledCount));
            OnPropertyChanged(nameof(NotInstalledCount));
            OnPropertyChanged(nameof(SummaryText));
        }

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
            LoadFiles();
        }

        public void Reload()
        {
            AllDownloadedFiles.Clear();
            FilteredDownloadedFiles.Clear();
            LoadFiles();
        }

        private void LoadFiles()
        {
            var metadataPath = PathConfig.DownloadedMods;          // downloaded_mods.json
            var installPath = PathConfig.InstalledGameFiles;       // installed_game_files.json

            // Ensure app data dir exists and migrate legacy metadata if present
            try
            {
                Directory.CreateDirectory(PathConfig.AppDataRoot);
                if (!File.Exists(metadataPath) && File.Exists(PathConfig.LegacyDownloadedMods))
                    File.Copy(PathConfig.LegacyDownloadedMods, metadataPath, overwrite: false);
                if (!File.Exists(installPath) && File.Exists(PathConfig.LegacyInstalledGameFiles))
                    File.Copy(PathConfig.LegacyInstalledGameFiles, installPath, overwrite: false);
            }
            catch { /* best effort */ }

            // Cache for category lookup
            var cachedMods = ModCacheService.LoadCachedMods() ?? new List<Mod>();
            var modsByName = cachedMods
                .GroupBy(m => m.Name, System.StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), System.StringComparer.OrdinalIgnoreCase);

            // Load downloaded metadata (optional)
            var downloadedList = File.Exists(metadataPath)
                ? (JsonSerializer.Deserialize<List<InstalledModInfo>>(File.ReadAllText(metadataPath)) ?? new())
                : new List<InstalledModInfo>();

            // Load install tracking (optional)
            var installedList = File.Exists(installPath)
                ? (JsonSerializer.Deserialize<List<InstalledGameFile>>(File.ReadAllText(installPath)) ?? new())
                : new List<InstalledGameFile>();

            // Index installed by (ModName, FileName[zip])
            var installedByKey = installedList.ToDictionary(
                igf => Key(igf.ModName, igf.FileName),
                igf => igf,
                System.StringComparer.OrdinalIgnoreCase);

            // 1) Start with everything in downloaded_mods.json
            foreach (var entry in downloadedList)
            {
                string modName = entry.ModName;
                string zipName = entry.FileName; // zip filename in downloaded_mods.json
                var settings = SettingsService.LoadSettings();
                string folder = Path.Combine(settings.OutputDir, PathUtils.SanitizeModName(modName));
                string zipPath = Path.Combine(folder, Path.GetFileName(zipName)); // exact zip name

                bool zipExists = File.Exists(zipPath);
                double sizeMB = zipExists ? new FileInfo(zipPath).Length / 1024.0 / 1024.0 : 0;

                bool isInstalled = installedByKey.ContainsKey(Key(modName, zipName));

                string category = "Unknown";
                if (modsByName.TryGetValue(modName, out var modFromCache) && !string.IsNullOrWhiteSpace(modFromCache.Category))
                    category = modFromCache.Category;

                // Skip only if neither installed nor the zip exists
                if (!isInstalled && !zipExists)
                    continue;

                AllDownloadedFiles.Add(new InstalledModDisplay
                {
                    ModName = modName,
                    FileName = zipName,
                    FileSizeMB = sizeMB,
                    UploadedTimestamp = entry.UploadedTimestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = isInstalled ? (zipExists ? "Installed" : "Installed (Missing Zip)") : "Not Installed",
                    Category = category,
                    IsMissingDownload = isInstalled && !zipExists
                });
            }

            // 2) Add installed-only items that are NOT in downloaded_mods.json
            var downloadedKeys = downloadedList
                .Select(d => Key(d.ModName, d.FileName))
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            foreach (var inst in installedList)
            {
                var key = Key(inst.ModName, inst.FileName);
                if (downloadedKeys.Contains(key))
                    continue;

                string category = "Unknown";
                if (modsByName.TryGetValue(inst.ModName, out var modFromCache) && !string.IsNullOrWhiteSpace(modFromCache.Category))
                    category = modFromCache.Category;

                AllDownloadedFiles.Add(new InstalledModDisplay
                {
                    ModName = inst.ModName,
                    FileName = inst.FileName,      // keep the zip filename from install tracking
                    FileSizeMB = 0,
                    UploadedTimestamp = "",                 // unknown here
                    Status = "Installed (Missing Zip)",
                    Category = category,
                    IsMissingDownload = true
                });
            }

            ApplyFilter();
            RefreshSummary();
        }

        private static string Key(string modName, string zipFileName) => $"{modName}|||{zipFileName}";

        private void ApplyFilter()
        {
            FilteredDownloadedFiles.Clear();

            foreach (var item in AllDownloadedFiles)
            {
                if (string.IsNullOrWhiteSpace(_searchText) ||
                    item.ModName.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase) ||
                    item.FileName.Contains(_searchText, System.StringComparison.OrdinalIgnoreCase))
                {
                    FilteredDownloadedFiles.Add(item);
                }
            }

            RefreshSummary();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // View-model row
    public class InstalledModDisplay
    {
        public string ModName { get; set; } = "";
        public string FileName { get; set; } = "";
        public double FileSizeMB { get; set; }
        public string FileSizeDisplay => FileSizeMB > 1 ? $"{FileSizeMB:F2} MB" : $"{FileSizeMB * 1024:F2} KB";
        public string UploadedTimestamp { get; set; } = "";
        public string Status { get; set; } = "Not Installed";
        public string Category { get; set; } = "Unknown";
        public bool IsMissingDownload { get; set; }
    }

    // Matches installed_game_files.json
    public class InstalledGameFile
    {
        public string ModName { get; set; } = "";
        public string FileName { get; set; } = ""; // zip filename
        public List<string> InstalledPaths { get; set; } = new(); // .archive paths
    }
}
