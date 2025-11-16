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
using System.Linq;

namespace LiteCyberpunkModManager.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private NexusApiService _apiService;
        private string _statusMessage = "Ready.";
        private string _lastLoadedSlug = string.Empty;

        public ObservableCollection<ModDisplay> Mods { get; set; } = new();
        public ListCollectionView ModsGrouped { get; set; }

        public int TotalModsCount =>
    ModsGrouped?.Cast<ModDisplay>().Count() ?? 0;

        public int NotDownloadedCount =>
            ModsGrouped?.Cast<ModDisplay>().Count(m =>
                m.Status.Equals("Not Downloaded", StringComparison.OrdinalIgnoreCase)) ?? 0;

        public int UpdateAvailableCount =>
            ModsGrouped?.Cast<ModDisplay>().Count(m =>
                m.Status.Equals("Update Available!", StringComparison.OrdinalIgnoreCase)) ?? 0;

        public string SummaryText =>
    $"{NotDownloadedCount} not downloaded, {UpdateAvailableCount} with updates available";


        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(TotalModsCount));
            OnPropertyChanged(nameof(NotDownloadedCount));
            OnPropertyChanged(nameof(UpdateAvailableCount));
            OnPropertyChanged(nameof(SummaryText));
        }

        public void RefreshApiService(NexusApiService api)
        {
            _apiService = api;
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _statusFilter = "All statuses";
        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (_statusFilter == value) return;
                _statusFilter = value;
                OnPropertyChanged();
                ApplyFilters(); // <- re-apply when status changes
            }
        }

        private void ApplyFilters()
        {
            ModsGrouped.Filter = obj =>
            {
                if (obj is not ModDisplay mod) return false;

                bool matchesSearch = string.IsNullOrWhiteSpace(SearchText) ||
                                     (mod.Name?.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0);

                bool matchesCategory = SelectedCategory == "All" || SelectedCategory == null ||
                                       string.Equals(mod.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase);

                var status = mod.Status ?? string.Empty;
                bool matchesStatus = _statusFilter switch
                {
                    "Latest Downloaded" => status.Equals("Latest Downloaded", StringComparison.OrdinalIgnoreCase),
                    "Downloaded" => status.Equals("Downloaded", StringComparison.OrdinalIgnoreCase),
                    "Not Downloaded" => status.Equals("Not Downloaded", StringComparison.OrdinalIgnoreCase),
                    "Update Available!" => status.Equals("Update Available!", StringComparison.OrdinalIgnoreCase),
                    _ => true,
                };

                return matchesSearch && matchesCategory && matchesStatus;
            };

            ModsGrouped.Refresh();
            RefreshSummary(); // <— added
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
                        var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
                        remoteFiles = await _apiService.GetModFilesAsync(slug, mod.ModId);
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
            RefreshSummary(); // <— added

            StatusMessage = $"Mods loaded ({modDisplays.Length}).";
        }


        private string GetUpdateStatus(int modId, List<ModFile> latestFiles, List<InstalledModInfo> installedFiles)
        {
            var installedForMod = installedFiles.Where(f => f.ModId == modId).ToList();
            if (!installedForMod.Any())
            {
                Debug.WriteLine($"[StatusCheck] ModId {modId}: No installed files. -> Not Downloaded");
                return "Not Downloaded";
            }

            if (latestFiles == null || latestFiles.Count == 0)
            {
                Debug.WriteLine($"[StatusCheck] ModId {modId}: No remote files. -> Downloaded");
                return "Downloaded";
            }

            var newestRemote = latestFiles
                .OrderByDescending(f => f.UploadedTimestamp)
                .FirstOrDefault();

            if (newestRemote == null)
            {
                Debug.WriteLine($"[StatusCheck] ModId {modId}: No newest remote file found. -> Downloaded");
                return "Downloaded";
            }

            Debug.WriteLine($"[StatusCheck] ModId {modId}: Newest remote file: {newestRemote.FileName} (FileId: {newestRemote.FileId}, Uploaded: {newestRemote.UploadedTimestamp:O})");

            // Latest Downloaded: newest remote file is installed
            bool hasLatest = installedForMod.Any(inst =>
                inst.FileId == newestRemote.FileId ||
                (inst.FileName.Equals(newestRemote.FileName, StringComparison.OrdinalIgnoreCase) &&
                 inst.UploadedTimestamp == newestRemote.UploadedTimestamp));

            if (hasLatest)
            {
                Debug.WriteLine($"[StatusCheck] -> Latest Downloaded");
                return "Latest Downloaded";
            }

            // Update Available!: newer file with same filename exists
            foreach (var installed in installedForMod)
            {
                var newerSameNameRemote = latestFiles.FirstOrDefault(remote =>
                    remote.FileName.Equals(installed.FileName, StringComparison.OrdinalIgnoreCase) &&
                    remote.UploadedTimestamp > installed.UploadedTimestamp);

                if (newerSameNameRemote != null)
                {
                    Debug.WriteLine($"[StatusCheck] -> Update Available! (newer file with same filename {installed.FileName})");
                    return "Update Available!";
                }
            }

            // Downloaded: some file installed, newer files exist but with different names
            bool anyNewerOverall = latestFiles.Any(remote =>
                installedForMod.All(inst => remote.UploadedTimestamp > inst.UploadedTimestamp));

            if (anyNewerOverall)
            {
                Debug.WriteLine($"[StatusCheck] -> Downloaded (installed, but newer alternative files exist)");
                return "Downloaded";
            }

            // Fallback: treat as Downloaded
            Debug.WriteLine($"[StatusCheck] -> Downloaded (fallback)");
            return "Downloaded";
        }


        public async Task UpdateModStatusAsync(int modId)
        {
            var installed = LoadInstalledMetadata(); // reloads from disk every time
            var slug2 = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
            var remoteFiles = await _apiService.GetModFilesAsync(slug2, modId);

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
            RefreshSummary(); // <— added
        }

        private async Task<List<Mod>?> TryFetchFromApiAsync()
        {
            // reset rate-limit warning flag before retry
            typeof(NexusApiService)
                .GetField("_localRateLimitShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                ?.SetValue(null, false);

            try
            {
                var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
                var mods = await _apiService.GetTrackedModsAsync(slug);
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
                var list = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();
                var selectedGame = SettingsService.LoadSettings().SelectedGame;
                return list.Where(x => x.Game == selectedGame).ToList();
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

            var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);

            // If the selected game changed since last load, force an API fetch
            if (!string.Equals(_lastLoadedSlug, slug, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Game changed — fetching from Nexus API...";
                var fresh = await TryFetchFromApiAsync();
                if (fresh != null && fresh.Count > 0)
                {
                    await PopulateModsAsync(fresh);
                    _lastLoadedSlug = slug;
                    return;
                }
            }

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
            _lastLoadedSlug = slug;
        }


        public async Task LoadTrackedModsFromApiFirstAsync()
        {
            StatusMessage = "Fetching mods from Nexus API...";
            Mods.Clear();

            var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
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
            _lastLoadedSlug = slug;
        }


        public void RefreshModList()
        {
            ModsGrouped.Refresh();
            RefreshSummary(); // <— added
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
