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

        public async Task LoadTrackedModsAsync()
        {
            StatusMessage = "Fetching mods from Nexus API...";

            // ✅ Reset rate-limit popup for this fetch attempt
            typeof(NexusApiService)
                .GetField("_localRateLimitShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.SetValue(null, false);

            Mods.Clear();

            List<Mod>? mods = null;

            try
            {
                mods = await _apiService.GetTrackedModsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Error fetching from API: {ex.Message}");
            }

            if (mods == null || mods.Count == 0)
            {
                StatusMessage = "Loading cached mods...";
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
                ModCacheService.SaveCachedMods(mods);
                StatusMessage = "Mods fetched and cached.";
            }

            var installed = LoadInstalledMetadata();

            var tasks = mods.OrderBy(m => m.Category).ThenBy(m => m.Name)
                .Select(async mod =>
                {
                    string status = "Not Downloaded";
                    var remoteFiles = await _apiService.GetModFilesAsync("cyberpunk2077", mod.ModId);

                    if (installed.Any(i => i.ModId == mod.ModId))
                    {
                        status = GetUpdateStatus(mod.ModId, remoteFiles, installed);
                    }

                    return new ModDisplay
                    {
                        Name = mod.Name,
                        ModId = mod.ModId,
                        Category = mod.Category,
                        Status = status
                    };
                });

            var modDisplays = await Task.WhenAll(tasks);

            foreach (var modDisplay in modDisplays)
            {
                Mods.Add(modDisplay);
            }

            StatusMessage = "Mods loaded.";
        }



        private List<InstalledModInfo> LoadInstalledMetadata()
        {
            var metadataPath = Path.Combine(Settings.DefaultModsDir, "installed_mods.json");
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

            // Check for matching filename updates
            foreach (var installed in installedForMod)
            {
                var newerSameName = latestFiles.FirstOrDefault(remote =>
                    remote.FileName.Equals(installed.FileName, StringComparison.OrdinalIgnoreCase) &&
                    remote.UploadedTimestamp > installed.UploadedTimestamp);

                if (newerSameName != null)
                    return "Update Available!";
            }

            // If any installed file is the latest by timestamp
            var newestRemote = latestFiles.OrderByDescending(f => f.UploadedTimestamp).FirstOrDefault();
            if (newestRemote != null && installedForMod.Any(i =>
                i.FileName.Equals(newestRemote.FileName, StringComparison.OrdinalIgnoreCase) &&
                i.UploadedTimestamp == newestRemote.UploadedTimestamp))
            {
                return "Latest Downloaded";
            }

            // Default case
            return "Downloaded";
        }

        public async Task UpdateModStatusAsync(int modId)
        {
            var installed = LoadInstalledMetadata(); // Reloads from disk every time
            var remoteFiles = await _apiService.GetModFilesAsync("cyberpunk2077", modId);

            var modDisplay = Mods.FirstOrDefault(m => m.ModId == modId);
            if (modDisplay == null) return;

            if (installed.Any(i => i.ModId == modId))
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