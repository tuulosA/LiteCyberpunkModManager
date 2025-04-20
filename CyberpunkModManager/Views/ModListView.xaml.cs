using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;
using CyberpunkModManager.ViewModels;
using CyberpunkModManager.Views;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace CyberpunkModManager.Views
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
            string metadataPath = Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");

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


        private async void ManageFiles_Click(object sender, RoutedEventArgs e)
        {
            if (ModsListView.SelectedItem is not ModDisplay selected)
            {
                MessageBox.Show("Please select a mod to manage files for.", "No Mod Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string metadataPath = Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");
            if (!File.Exists(metadataPath))
            {
                MessageBox.Show("No downloaded files found for this mod.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var modEntries = metadataList.Where(m => m.ModId == selected.ModId).ToList();
            if (modEntries.Count == 0)
            {
                MessageBox.Show("No downloaded files found for this mod.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            HashSet<string> allFiles = new();
            foreach (var entry in modEntries)
            {
                string folderName = PathUtils.SanitizeModName(entry.ModName);
                string folderPath = Path.Combine(Settings.DefaultModsDir, folderName);

                if (Directory.Exists(folderPath))
                {
                    foreach (var file in Directory.GetFiles(folderPath))
                    {
                        allFiles.Add(file);
                    }
                }
            }

            if (allFiles.Count == 0)
            {
                MessageBox.Show("No downloaded files found for this mod.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new ManageFilesWindow(allFiles.ToList());
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedFiles.Count > 0)
            {
                List<string> deletedFileNames = new();

                foreach (var filePath in dialog.SelectedFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    string fileNameNoExt = Path.GetFileNameWithoutExtension(fileName);

                    var matchingEntry = metadataList.FirstOrDefault(entry =>
                        entry.ModId == selected.ModId &&
                        Path.GetFileNameWithoutExtension(entry.FileName)
                            .Equals(fileNameNoExt, StringComparison.OrdinalIgnoreCase));

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

                bool hasRemaining = metadataList.Any(e => e.ModId == selected.ModId);
                if (!hasRemaining)
                {
                    selected.Status = "Not Downloaded";
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
                await _viewModel.UpdateModStatusAsync(selected.ModId);

                if (Application.Current.MainWindow is MainWindow mainWindow &&
                    mainWindow.FindName("FilesTabContent") is ContentControl filesTab &&
                    filesTab.Content is FilesView filesView)
                {
                    filesView.RefreshFileList();
                }
            }
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