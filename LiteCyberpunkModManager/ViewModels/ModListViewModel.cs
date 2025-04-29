using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Data;
using LiteCyberpunkModManager.Helpers;
using System.Diagnostics;

namespace LiteCyberpunkModManager.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private NexusApiService _apiService;
        private string _statusMessage = "Ready.";

        public ObservableCollection<ModDisplay> Mods { get; set; } = new();
        public ListCollectionView ModsGrouped { get; set; }

        public void RefreshApiService(NexusApiService api)
        {
            _apiService = api;
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private void ApplyFilters()
        {
            ModsGrouped.Filter = obj =>
            {
                if (obj is not ModDisplay mod) return false;

                bool matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                                     mod.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

                bool matchesCategory = SelectedCategory == "All" || SelectedCategory == null ||
                                       mod.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase);

                return matchesSearch && matchesCategory;
            };

            ModsGrouped.Refresh();
        }


        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        private string? _selectedCategory;
        public string? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public ObservableCollection<string> AvailableCategories { get; set; } = new();


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
                        // silently ignore fetch failure
                    }

                    if (fileCount > 0)
                    {
                        if (remoteFiles != null && remoteFiles.Count > 0)
                        {
                            status = GetUpdateStatus(mod.ModId, remoteFiles, installed);
                        }
                        else
                        {
                            status = "Downloaded";
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

            Mods.Clear();
            foreach (var modDisplay in modDisplays)
            {
                Mods.Add(modDisplay);
            }

            // update available categories for filtering
            var distinctCategories = modDisplays
                .Select(m => m.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            AvailableCategories.Clear();
            AvailableCategories.Add("All");

            foreach (var category in distinctCategories)
            {
                AvailableCategories.Add(category);
            }

            SelectedCategory = "All";
            ApplyFilters();

            StatusMessage = $"Mods loaded ({modDisplays.Length}).";
        }


        private string GetUpdateStatus(int modId, List<ModFile> latestFiles, List<InstalledModInfo> installedFiles)
        {
            var installedForMod = installedFiles.Where(f => f.ModId == modId).ToList();
            if (!installedForMod.Any() || latestFiles.Count == 0)
            {
                Debug.WriteLine($"[StatusCheck] ModId {modId}: No installed files or no remote files. -> Not Downloaded");
                return "Not Downloaded";
            }

            var newestRemote = latestFiles.OrderByDescending(f => f.UploadedTimestamp).FirstOrDefault();
            if (newestRemote == null)
            {
                Debug.WriteLine($"[StatusCheck] ModId {modId}: No newest remote file found. -> Downloaded");
                return "Downloaded";
            }

            Debug.WriteLine($"[StatusCheck] ModId {modId}: Newest remote file: {newestRemote.FileName} (Uploaded: {newestRemote.UploadedTimestamp:O})");

            foreach (var installed in installedForMod)
            {
                Debug.WriteLine($"[StatusCheck] Checking installed file: {installed.FileName} (Uploaded: {installed.UploadedTimestamp:O})");

                if ((installed.FileName.Equals(newestRemote.FileName, StringComparison.OrdinalIgnoreCase) ||
                     installed.FileName.Equals(newestRemote.FileName + ".zip", StringComparison.OrdinalIgnoreCase)) &&
                    installed.UploadedTimestamp == newestRemote.UploadedTimestamp)

                {
                    Debug.WriteLine($"[StatusCheck] -> MATCH: Installed file matches newest remote file exactly -> Latest Downloaded");
                    return "Latest Downloaded";
                }
            }

            bool isUpdateAvailable = installedForMod.Any(inst => inst.UploadedTimestamp < newestRemote.UploadedTimestamp);

            if (isUpdateAvailable)
            {
                Debug.WriteLine($"[StatusCheck] -> UPDATE: Installed files are older than newest remote -> Update Available!");
                return "Update Available!";
            }

            Debug.WriteLine($"[StatusCheck] -> FALLBACK: Installed file(s) but not latest, no update -> Downloaded");
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
                Debug.WriteLine($"[WARN] Error fetching from API: {ex.Message}");
                return null;
            }
        }


        private List<InstalledModInfo> LoadInstalledMetadata()
        {
            var metadataPath = PathConfig.DownloadedMods;
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
                StatusMessage = "Loaded mods from cache. Checking for updates...";
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