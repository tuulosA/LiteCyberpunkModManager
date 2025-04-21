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

namespace LiteCyberpunkModManager.Views
{
    public partial class ModListView : UserControl
    {
        private readonly ModListViewModel _viewModel;
        private readonly Settings _settings;
        private readonly NexusApiService _api;

        public ModListView()
        {
            InitializeComponent();
            _settings = SettingsService.LoadSettings();
            _api = new NexusApiService(_settings.NexusApiKey);
            _viewModel = new ModListViewModel(_api);
            DataContext = _viewModel;

            App.GlobalModListViewModel = _viewModel;

            Loaded += ModListView_Loaded;
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

        private async void ModListView_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ModListView_Loaded; // run only once

            await _viewModel.LoadTrackedModsFromCacheFirstAsync(); // startup
        }

        private async void FetchMods_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadTrackedModsFromApiFirstAsync(); // user-triggered fetch

        }

        private async void DownloadFiles_Click(object sender, RoutedEventArgs e)
        {
            if (ModsListView.SelectedItem is not ModDisplay selected)
            {
                MessageBox.Show("Please select a mod from the list.", "No Mod Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var modId = selected.ModId;
            Console.WriteLine($"[DEBUG] Fetching files for Mod ID: {modId}");

            var files = await _api.GetModFilesAsync("cyberpunk2077", modId);

            if (files == null)
            {
                Console.WriteLine("[DEBUG] GetModFilesAsync returned null.");
                MessageBox.Show("Failed to fetch files for this mod.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Console.WriteLine($"[DEBUG] Fetched {files.Count} files.");
            if (files.Count == 0)
            {
                MessageBox.Show("No files found for this mod.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            List<InstalledModInfo> alreadyDownloaded = new();
            string metadataPath = PathConfig.DownloadedMods;

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
                    Console.WriteLine($"[WARN] Failed to load existing mod metadata: {ex.Message}");
                }
            }

            var dialog = new DownloadFileWindow(files, alreadyDownloaded, modId, selected.Name, _viewModel);
            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                await _viewModel.UpdateModStatusAsync(modId);
                MessageBox.Show("Download completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                Console.WriteLine($"[WARN] Failed to load downloaded_mods.json: {ex.Message}");
                return;
            }

            List<string> filePaths = new();
            ModDisplay? selectedMod = null;

            if (ModsListView.SelectedItem is ModDisplay selected)
            {
                selectedMod = selected;
                var modEntries = metadataList.Where(m => m.ModId == selectedMod.ModId).ToList();
                if (modEntries.Count == 0)
                {
                    MessageBox.Show("No downloaded files found for this mod.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                filePaths = GetPathsForEntries(modEntries);
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

            var dialog = new ManageFilesWindow(filePaths);
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedFiles.Count > 0)
            {
                var deletedFileNames = new List<string>();

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
                        Console.WriteLine($"Removed metadata entry for file: {fileName} (ID: {matchingEntry.FileId})");
                    }

                    try
                    {
                        File.Delete(filePath);
                        Console.WriteLine($"Deleted: {filePath}");
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
                    Console.WriteLine($"[ERROR] Failed to update downloaded_mods.json: {ex.Message}");
                }

                string summary = deletedFileNames.Count > 0
                    ? "Deleted files:\n- " + string.Join("\n- ", deletedFileNames)
                    : "No matching metadata entries were found to remove.";

                MessageBox.Show(summary, "Files Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                if (selectedMod != null)
                    await _viewModel.UpdateModStatusAsync(selectedMod.ModId);

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
                string folderPath = Path.Combine(Settings.DefaultModsDir, folderName);

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
                string url = $"https://www.nexusmods.com/cyberpunk2077/mods/{modId}?tab=files";

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


    }
}