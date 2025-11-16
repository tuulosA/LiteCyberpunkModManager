using System.Windows;
using System.Windows.Controls;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;
using LiteCyberpunkModManager.ViewModels;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using LiteCyberpunkModManager.Helpers;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Data;
using System.Threading.Tasks;

namespace LiteCyberpunkModManager.Views
{
    public partial class ModListView : UserControl
    {
        private readonly ModListViewModel _viewModel;
        private readonly Settings _settings;
        private readonly NexusApiService _api;
        private ICollectionView? _modsView;
        private string _statusFilter = "All statuses";

        public ModListView()
        {
            InitializeComponent();
            _settings = SettingsService.LoadSettings();
            _api = new NexusApiService(_settings.NexusApiKey);
            _api.NotificationRaised += n =>
            {
                var icon = n.Type switch
                {
                    NotificationType.Error => MessageBoxImage.Error,
                    NotificationType.Warning => MessageBoxImage.Warning,
                    _ => MessageBoxImage.Information
                };
                MessageBox.Show(n.Message, n.Title, MessageBoxButton.OK, icon);
            };
            _viewModel = new ModListViewModel(_api);
            DataContext = _viewModel;

            App.GlobalModListViewModel = _viewModel;

            Loaded += ModListView_Loaded;
        }

        private void SaveMassDownloadMetadata(int modId, string modName, int fileId, string fileName, DateTime uploadedTimestamp)
        {
            try
            {
                string metadataPath = PathConfig.DownloadedMods;
                Directory.CreateDirectory(PathConfig.AppDataRoot);

                var entry = new InstalledModInfo
                {
                    ModId = modId,
                    ModName = modName,
                    FileId = fileId,
                    FileName = fileName,
                    UploadedTimestamp = uploadedTimestamp,
                    Game = SettingsService.LoadSettings().SelectedGame
                };

                List<InstalledModInfo> list = new();
                if (File.Exists(metadataPath))
                {
                    try
                    {
                        string json = File.ReadAllText(metadataPath);
                        list = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WARN] Could not read existing mass-download metadata: {ex.Message}");
                    }
                }

                list.RemoveAll(m => m.ModId == modId && m.FileName.Equals(fileName, System.StringComparison.OrdinalIgnoreCase));
                list.Add(entry);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(metadataPath, JsonSerializer.Serialize(list, options));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to persist mass-download metadata: {ex.Message}");
            }
        }

        private void EnsureModsViewHooked()
        {
            if (ModsListView == null) return;

            var currentSource = ModsListView.ItemsSource;

            // If ItemsSource is gone, drop the old view.
            if (currentSource == null)
            {
                _modsView = null;
                return;
            }

            // (Re)attach if we never attached OR the ItemsSource has changed.
            if (_modsView == null || !ReferenceEquals(_modsView.SourceCollection, currentSource))
            {
                _modsView = CollectionViewSource.GetDefaultView(currentSource);
                if (_modsView != null)
                    _modsView.Filter = ModStatusFilterPredicate;
            }
        }

        private bool ModStatusFilterPredicate(object? obj)
        {
            if (obj is not ModDisplay mod) return true;
            var status = mod.Status ?? string.Empty;

            switch (_statusFilter)
            {
                case "Latest Downloaded":
                    return status.Equals("Latest Downloaded", StringComparison.OrdinalIgnoreCase);
                case "Downloaded":
                    return status.Equals("Downloaded", StringComparison.OrdinalIgnoreCase);
                case "Not Downloaded":
                    return status.Equals("Not Downloaded", StringComparison.OrdinalIgnoreCase);
                case "Update Available!":
                    return status.Equals("Update Available!", StringComparison.OrdinalIgnoreCase);
                case "All statuses":
                default:
                    return true;
            }
        }

        private void RefreshStatusFilter()
        {
            EnsureModsViewHooked();
            _modsView?.Refresh();
        }

        // Opens the header's context menu when the "Status" header is clicked
        private void StatusHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border b && b.ContextMenu != null)
            {
                b.ContextMenu.PlacementTarget = b;
                b.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        // Keeps a checkmark on the active status filter
        private void StatusHeader_ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu cm)
            {
                foreach (var item in cm.Items)
                {
                    if (item is MenuItem mi && mi.Header is string text)
                    {
                        mi.IsCheckable = true;
                        mi.IsChecked = string.Equals(_statusFilter, text, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        // Applies the selected status filter from the header menu
        private void StatusHeaderMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Header is string choice)
            {
                _statusFilter = choice;          // keep the menu checkmark correct
                _viewModel.StatusFilter = choice;
                RefreshStatusFilter();           // optional
            }
        }


        private async void ModListView_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ModListView_Loaded;

            await _viewModel.LoadTrackedModsFromCacheFirstAsync();

            // ensure the ListView field is created & bound before we grab its view
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            EnsureModsViewHooked();
        }


        private async void FetchMods_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadTrackedModsFromApiFirstAsync(); // user-triggered fetch
            EnsureModsViewHooked(); // NEW: re-attach if ItemsSource swapped
        }


        public void ReinitializeApiService()
        {
            var updatedSettings = SettingsService.LoadSettings();
            _api.SetApiKey(updatedSettings.NexusApiKey);
            _viewModel.RefreshApiService(_api);
        }

        public async Task FetchModsFromApiAsync()
        {
            await _viewModel.LoadTrackedModsFromApiFirstAsync();
        }

        private void OpenTrackingCentre_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.nexusmods.com/mods/trackingcentre",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open Tracking Centre.\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DownloadFiles_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = ModsListView.SelectedItems.Cast<ModDisplay>().ToList();

            if (selectedMods.Count == 0)
            {
                MessageBox.Show("Please select at least one mod from the list.", "No Mod Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var selected in selectedMods)
            {
                var modId = selected.ModId;
                Debug.WriteLine($"[DEBUG] Fetching files for Mod ID: {modId}");

                var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
                var files = await _api.GetModFilesAsync(slug, modId);
                if (files == null)
                {
                    Debug.WriteLine("[DEBUG] GetModFilesAsync returned null.");
                    MessageBox.Show($"Failed to fetch files for mod: {selected.Name}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    continue;
                }

                if (files.Count == 0)
                {
                    MessageBox.Show($"No files found for mod: {selected.Name}", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                    continue;
                }

                List<InstalledModInfo> alreadyDownloaded = new();
                string metadataPath = PathConfig.DownloadedMods;
                Directory.CreateDirectory(PathConfig.AppDataRoot);

                if (File.Exists(metadataPath))
                {
                    try
                    {
                        string json = File.ReadAllText(metadataPath);
                        alreadyDownloaded = JsonSerializer.Deserialize<List<InstalledModInfo>>(json)?
                            .Where(m => m.ModId == selected.ModId)
                            .ToList() ?? new();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WARN] Failed to load existing mod metadata: {ex.Message}");
                    }
                }

                var dialog = new DownloadFileWindow(files, alreadyDownloaded, modId, selected.Name, _viewModel)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    await _viewModel.UpdateModStatusAsync(modId);
                    MessageBox.Show($"Download completed for {selected.Name}.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }


        private void ModsListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // avoid interfering with double click behavior
            if (e.ClickCount > 1)
                return;

            var item = ItemsControl.ContainerFromElement(ModsListView, e.OriginalSource as DependencyObject) as ListViewItem;
            if (item != null && item.IsSelected)
            {
                // unselect if already selected
                ModsListView.SelectedItem = null;
                e.Handled = true;
            }
        }

        private async void ManageFiles_Click(object sender, RoutedEventArgs e)
        {
            string metadataPath = PathConfig.DownloadedMods;
            Directory.CreateDirectory(PathConfig.AppDataRoot);
            if (!File.Exists(metadataPath))
            {
                MessageBox.Show("No downloaded files found.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<InstalledModInfo> metadataList;
            try
            {
                string json = File.ReadAllText(metadataPath);
                metadataList = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WARN] Failed to load downloaded_mods.json: {ex.Message}");
                return;
            }

            var selectedMods = ModsListView.SelectedItems.Cast<ModDisplay>().ToList();

            List<string> filePaths;
            if (selectedMods.Count > 0)
            {
                var selectedModIds = selectedMods.Select(m => m.ModId).ToList();
                var selectedEntries = metadataList.Where(m => selectedModIds.Contains(m.ModId)).ToList();

                if (selectedEntries.Count == 0)
                {
                    MessageBox.Show("No downloaded files found for selected mod(s).", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                filePaths = GetPathsForEntries(selectedEntries);
            }
            else
            {
                filePaths = GetPathsForEntries(metadataList);
                if (filePaths.Count == 0)
                {
                    MessageBox.Show("No downloaded files found.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            var dialog = new ManageFilesWindow(filePaths)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedFiles.Count > 0)
            {
                var deletedFileNames = new List<string>();
                var affectedModIds = new HashSet<int>();

                foreach (var filePath in dialog.SelectedFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);

                    var matchingEntry = metadataList.FirstOrDefault(entry =>
                        Path.GetFileNameWithoutExtension(entry.FileName).Equals(fileNameNoExt, StringComparison.OrdinalIgnoreCase));

                    if (matchingEntry != null)
                    {
                        metadataList.Remove(matchingEntry);
                        deletedFileNames.Add(fileName);
                        affectedModIds.Add(matchingEntry.ModId);
                        Debug.WriteLine($"Removed metadata entry for file: {fileName} (ID: {matchingEntry.FileId})");
                    }

                    try
                    {
                        File.Delete(filePath);
                        Debug.WriteLine($"Deleted: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete file: {filePath}\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadataList, options));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to update downloaded_mods.json: {ex.Message}");
                }

                string summary = deletedFileNames.Count > 0
                    ? "Deleted files:\n- " + string.Join("\n- ", deletedFileNames)
                    : "No matching metadata entries were found to remove.";

                MessageBox.Show(summary, "Files Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                // update status of affected mods
                foreach (var modId in affectedModIds)
                {
                    await _viewModel.UpdateModStatusAsync(modId);
                }

                // refresh the file list
                if (Application.Current.MainWindow is MainWindow mainWindow &&
                    mainWindow.FindName("FilesTabContent") is ContentControl filesTab &&
                    filesTab.Content is FilesView filesView)
                {
                    filesView.RefreshFileList();
                }
            }
        }


        private List<string> GetPathsForEntries(List<InstalledModInfo> entries)
        {
            var seenFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                string folderName = PathUtils.SanitizeModName(entry.ModName);
                string folderPath = Path.Combine(SettingsService.LoadSettings().OutputDir, folderName);

                // avoid scanning the same folder multiple times
                if (!seenFolders.Add(folderPath)) continue;

                if (Directory.Exists(folderPath))
                {
                    foreach (var file in Directory.GetFiles(folderPath))
                    {
                        filePaths.Add(file);
                    }
                }
            }

            return filePaths.ToList();
        }


        private void ModsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ModsListView.SelectedItem is ModDisplay selected)
            {
                int modId = selected.ModId;
                string modName = selected.Name;
                var pageSlug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
                string url = $"https://www.nexusmods.com/{pageSlug}/mods/{modId}?tab=files";

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open mod page.\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private async void ImportAndDownloadAll_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Select a Modlist JSON File"
            };

            if (openFileDialog.ShowDialog() != true) return;

            string json;
            try
            {
                json = File.ReadAllText(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read modlist file.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // parse the mod list
            List<(int ModId, string ModName, int FileId, string FileName)> modsToDownload;
            try
            {
                var doc = JsonDocument.Parse(json);
                modsToDownload = doc.RootElement
                    .EnumerateArray()
                    .Select(e => (
                        e.GetProperty("ModId").GetInt32(),
                        e.GetProperty("ModName").GetString()!,
                        e.GetProperty("FileId").GetInt32(),
                        e.GetProperty("FileName").GetString()!
                    )).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Invalid modlist format.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var selectedGame = SettingsService.LoadSettings().SelectedGame;
            var slugForIds = GameHelper.GetNexusSlug(selectedGame);
            var trackedModIds = await _api.GetTrackedModIdsAsync(slugForIds);
            if (!modsToDownload.All(m => trackedModIds.Contains(m.ModId)))
            {
                MessageBox.Show("The mod list does not match your currently tracked mods.\nPlease import the list first in Settings.", "Modlist Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var isPremium = await _api.IsPremiumUserAsync();
            if (!isPremium)
            {
                MessageBox.Show("Mass downloading requires a Nexus Mods Premium account.", "Premium Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var progressWindow = new MassDownloadBarWindow();
            progressWindow.SetModName("Preparing downloads...");
            progressWindow.SetFileName(string.Empty);
            progressWindow.SetFileCounter(0, modsToDownload.Count);
            progressWindow.SetStatusText("Fetching metadata...");
            progressWindow.SetOverallProgress(0);
            progressWindow.SetFileProgress(0);
            progressWindow.Show();
            await Task.Yield();

            // Pre-fetch remote file metadata so we know the real upload timestamps
            var uploadMap = new Dictionary<(int ModId, int FileId), DateTime>();
            int metadataCount = 0;
            foreach (var group in modsToDownload.GroupBy(m => m.ModId))
            {
                try
                {
                    progressWindow.SetStatusText($"Fetching metadata ({metadataCount}/{modsToDownload.Count})...");
                    var remoteFiles = await _api.GetModFilesAsync(slugForIds, group.Key);
                    foreach (var f in remoteFiles)
                    {
                        uploadMap[(group.Key, f.FileId)] = f.UploadedTimestamp;
                    }
                    metadataCount += group.Count();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WARN] Failed to prefetch files for mod {group.Key}: {ex.Message}");
                }
            }
            progressWindow.SetStatusText("Starting downloads...");

            int total = modsToDownload.Count;
            int completed = 0;
            int failed = 0;

            for (int i = 0; i < total; i++)
            {
                var (modId, modName, fileId, fileName) = modsToDownload[i];
                if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".zip";
                }

                Debug.WriteLine($"[INFO] Processing {modName} (ModId: {modId}, FileId: {fileId})");

                progressWindow.SetModName(modName);
                progressWindow.SetFileName(fileName);
                progressWindow.SetFileCounter(i + 1, total);
                progressWindow.SetFileProgress(0); // reset file progress
                progressWindow.SetOverallProgress((double)completed / total * 100);
                progressWindow.SetStatusText("Getting download link...");

                string? downloadUrl = await _api.GetDownloadLinkAsync(slugForIds, modId, fileId);
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    Debug.WriteLine($"[WARN] No download URL found for {modName}");
                    failed++;
                    completed++;
                    continue;
                }

                string folder = Path.Combine(SettingsService.LoadSettings().OutputDir, PathUtils.SanitizeModName(modName));
                Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, fileName);

                progressWindow.SetStatusText("Downloading...");

                var progress = new Progress<double>(value =>
                {
                    progressWindow.SetFileProgress(value);

                    double overall = (completed + value / 100.0) / total * 100;
                    progressWindow.SetOverallProgress(overall);
                });

                bool success = await _api.DownloadFileAsync(downloadUrl, filePath, progress);
                if (success)
                {
                    Debug.WriteLine($"[SUCCESS] Downloaded {modName}");
                    DateTime uploadedTs = uploadMap.TryGetValue((modId, fileId), out var ts)
                        ? ts
                        : DateTime.UtcNow;
                    SaveMassDownloadMetadata(modId, modName, fileId, fileName, uploadedTs);
                    await _viewModel.UpdateModStatusAsync(modId);
                }
                else
                {
                    Debug.WriteLine($"[FAIL] Failed to download {modName}");
                    failed++;
                }

                completed++;
            }

            progressWindow.Close();

            MessageBox.Show($"Mass download complete.\nSuccess: {completed - failed}, Failed: {failed}", "Done", MessageBoxButton.OK, MessageBoxImage.Information);

            if (Application.Current.MainWindow is MainWindow mainWindow &&
                mainWindow.FindName("FilesTabContent") is ContentControl filesTab &&
                filesTab.Content is FilesView filesView)
            {
                filesView.RefreshFileList();
            }
        }


    }
}

