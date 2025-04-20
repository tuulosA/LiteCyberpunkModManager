using CyberpunkModManager.Models;
using CyberpunkModManager.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using CyberpunkModManager.Helpers;

namespace CyberpunkModManager.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private readonly NexusApiService _apiService;
        private string _statusMessage = "Ready.";

        public ObservableCollection<ModDisplay> Mods { get; set; } = new();
        public ListCollectionView ModsGrouped { get; set; }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }



        public ModListViewModel(NexusApiService apiService)
        {
            _apiService = apiService;
            ModsGrouped = new ListCollectionView(Mods);
            ModsGrouped.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
        }


        private async Task PopulateModsAsync(List<Mod> mods)
        {
            var installed = LoadInstalledMetadata();

            var tasks = mods.OrderBy(m => m.Category).ThenBy(m => m.Name)
                .Select(async mod =>
                {
                    string status = "Not Downloaded";
                    int fileCount = installed.Count(i => i.ModId == mod.ModId);

                    List<ModFile>? remoteFiles = null;

                    try
                    {
                        remoteFiles = await _apiService.GetModFilesAsync("cyberpunk2077", mod.ModId);
                    }
                    catch
                    {

                    }

                    if (fileCount > 0)
                    {
                        if (remoteFiles != null && remoteFiles.Count > 0)
                        {
                            status = GetUpdateStatus(mod.ModId, remoteFiles, installed);
                        }
                        else
                        {
                            status = "Downloaded"; // fallback when API fails but files are installed
                        }
                    }

                    return new ModDisplay
                    {
                        Name = mod.Name,
                        ModId = mod.ModId,
                        Category = mod.Category,
                        Status = status,
                        DownloadedFileCount = fileCount
                    };
                });

            var modDisplays = await Task.WhenAll(tasks);

            foreach (var modDisplay in modDisplays)
            {
                Mods.Add(modDisplay);
            }

            StatusMessage = $"Mods loaded ({modDisplays.Length}).";
        }





        public async Task LoadTrackedModsFromCacheFirstAsync()
        {
            StatusMessage = "Loading mods from cache...";
            Mods.Clear();

            List<Mod>? mods = ModCacheService.LoadCachedMods();

            if (mods == null || mods.Count == 0)
            {
                StatusMessage = "No cache found, fetching from Nexus API...";
                mods = await TryFetchFromApiAsync();
            }
            else
            {
                StatusMessage = "Loaded mods from cache.";
            }

            if (mods == null || mods.Count == 0)
            {
                StatusMessage = "No mods found in cache or API.";
                return;
            }

            await PopulateModsAsync(mods);
        }

        public async Task LoadTrackedModsFromApiFirstAsync()
        {
            StatusMessage = "Fetching mods from Nexus API...";
            Mods.Clear();

            var mods = await TryFetchFromApiAsync();

            if (mods == null || mods.Count == 0)
            {
                StatusMessage = "API fetch failed, loading from cache...";
                mods = ModCacheService.LoadCachedMods();

                if (mods == null || mods.Count == 0)
                {
                    StatusMessage = "No mods found in API or cache.";
                    return;
                }

                StatusMessage = "Loaded mods from cache.";
            }
            else
            {
                StatusMessage = "Mods fetched and cached.";
            }

            await PopulateModsAsync(mods);
        }


        private async Task<List<Mod>?> TryFetchFromApiAsync()
        {
            // reset rate-limit warning flag before retry
            typeof(NexusApiService)
                .GetField("_localRateLimitShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.SetValue(null, false);

            try
            {
                var mods = await _apiService.GetTrackedModsAsync();
                if (mods != null && mods.Count > 0)
                {
                    ModCacheService.SaveCachedMods(mods);
                }
                return mods;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Error fetching from API: {ex.Message}");
                return null;
            }
        }




        private List<InstalledModInfo> LoadInstalledMetadata()
        {
            var metadataPath = Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");
            if (!File.Exists(metadataPath)) return new();

            try
            {
                string json = File.ReadAllText(metadataPath);
                return JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }

        private string GetUpdateStatus(int modId, List<ModFile> latestFiles, List<InstalledModInfo> installedFiles)
        {
            var installedForMod = installedFiles.Where(f => f.ModId == modId).ToList();
            if (!installedForMod.Any() || latestFiles.Count == 0)
                return "Not Downloaded";

            // check for matching filename updates
            foreach (var installed in installedForMod)
            {
                var newerSameName = latestFiles.FirstOrDefault(remote =>
                    remote.FileName.Equals(installed.FileName, StringComparison.OrdinalIgnoreCase) &&
                    remote.UploadedTimestamp > installed.UploadedTimestamp);

                if (newerSameName != null)
                    return "Update Available!";
            }

            // if any installed file is the latest by timestamp
            var newestRemote = latestFiles.OrderByDescending(f => f.UploadedTimestamp).FirstOrDefault();
            if (newestRemote != null && installedForMod.Any(i =>
                i.FileName.Equals(newestRemote.FileName, StringComparison.OrdinalIgnoreCase) &&
                i.UploadedTimestamp == newestRemote.UploadedTimestamp))
            {
                return "Latest Downloaded";
            }

            return "Downloaded";
        }

        public async Task UpdateModStatusAsync(int modId)
        {
            var installed = LoadInstalledMetadata(); // reloads from disk every time
            var remoteFiles = await _apiService.GetModFilesAsync("cyberpunk2077", modId);

            var modDisplay = Mods.FirstOrDefault(m => m.ModId == modId);
            if (modDisplay == null) return;

            int fileCount = installed.Count(i => i.ModId == modId);
            modDisplay.DownloadedFileCount = fileCount;

            if (fileCount > 0)
            {
                modDisplay.Status = GetUpdateStatus(modId, remoteFiles, installed);
            }
            else
            {
                modDisplay.Status = "Not Downloaded";
            }

            RefreshModList();
        }



        public void RefreshModList()
        {
            ModsGrouped.Refresh();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private class InstalledModInfo
        {
            public int ModId { get; set; }
            public string ModName { get; set; } = "";
            public int FileId { get; set; }
            public string FileName { get; set; } = "";
            public System.DateTime UploadedTimestamp { get; set; }
        }
    }

    public class ModDisplay : INotifyPropertyChanged
    {
        private string _status = "Unknown";

        private int _downloadedFileCount;
        public int DownloadedFileCount
        {
            get => _downloadedFileCount;
            set { _downloadedFileCount = value; OnPropertyChanged(); }
        }

        public int ModId { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "Uncategorized";

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
