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
        }

        private async void FetchMods_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadTrackedModsAsync();
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
            foreach (var file in files)
            {
                Console.WriteLine($"[DEBUG] File ID: {file.FileId}, Name: {file.FileName}, Size: {file.FileSizeBytes}, Uploaded: {file.UploadedTimestamp}");
            }

            if (files.Count == 0)
            {
                MessageBox.Show("No files found for this mod.", "No Files", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // ✅ Load full installed metadata (not just names)
            List<InstalledModInfo> alreadyDownloaded = new();
            string metadataPath = Path.Combine(Settings.DefaultModsDir, "installed_mods.json");

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

            // ✅ Open the download dialog with full version-aware tracking
            var dialog = new DownloadFileWindow(files, alreadyDownloaded, modId);
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedFileIds.Count > 0)
            {
                bool anySuccess = false;

                foreach (var fileId in dialog.SelectedFileIds)
                {
                    var file = files.FirstOrDefault(f => f.FileId == fileId);
                    if (file == null)
                    {
                        Console.WriteLine($"[DEBUG] File ID {fileId} not found in list.");
                        continue;
                    }

                    Console.WriteLine($"[DEBUG] Requesting download link for file ID {fileId}");
                    var downloadUrl = await _api.GetDownloadLinkAsync("cyberpunk2077", modId, fileId);

                    if (downloadUrl == null)
                    {
                        Console.WriteLine($"[DEBUG] No download URL for file ID {fileId}");
                        MessageBox.Show($"No download link for file: {file.FileName}", "Download Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        continue;
                    }

                    string sanitizedModName = string.Join("_", selected.Name.Split(Path.GetInvalidFileNameChars()));
                    string modFolderPath = Path.Combine(Settings.DefaultModsDir, sanitizedModName);
                    Directory.CreateDirectory(modFolderPath);

                    var safeFileName = Path.GetFileName(file.FileName);
                    var savePath = Path.Combine(modFolderPath, safeFileName);
                    Console.WriteLine($"[DEBUG] Saving to: {savePath}");

                    var success = await _api.DownloadFileAsync(downloadUrl, savePath);
                    Console.WriteLine($"[DEBUG] Download success: {success}");

                    if (success)
                    {
                        SaveDownloadMetadata(modId, selected.Name, file);
                        anySuccess = true;

                        MessageBox.Show($"Downloaded '{file.FileName}' to Mods directory.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Failed to download '{file.FileName}'.", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                if (anySuccess)
                {
                    selected.Status = "Downloaded";
                    _viewModel.RefreshModList();
                }
            }
        }





        private void UninstallFiles_Click(object sender, RoutedEventArgs e)
        {
            if (ModsListView.SelectedItem is not ModDisplay selected)
            {
                MessageBox.Show("Please select a mod to uninstall files from.", "No Mod Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string sanitizedModName = string.Join("_", selected.Name.Split(Path.GetInvalidFileNameChars()));
            string modFolderPath = Path.Combine(Settings.DefaultModsDir, sanitizedModName);

            if (!Directory.Exists(modFolderPath))
            {
                MessageBox.Show("No installed files found for this mod.", "Nothing to Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var installedFiles = Directory.GetFiles(modFolderPath).ToList();
            if (installedFiles.Count == 0)
            {
                MessageBox.Show("No installed files found for this mod.", "Nothing to Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new UninstallFileWindow(installedFiles);
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedFiles.Count > 0)
            {
                string metadataPath = Path.Combine(Settings.DefaultModsDir, "installed_mods.json");
                List<InstalledModInfo> metadataList = new();
                List<string> uninstalledFileNames = new();

                if (File.Exists(metadataPath))
                {
                    try
                    {
                        string json = File.ReadAllText(metadataPath);
                        metadataList = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WARN] Failed to load installed_mods.json: {ex.Message}");
                    }
                }

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
                        uninstalledFileNames.Add(fileName);
                        Console.WriteLine($"Removed metadata entry for file: {fileName} (ID: {matchingEntry.FileId})");
                    }

                    // Delete the file from disk
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

                // Update mod status if necessary
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
                    Console.WriteLine($"[ERROR] Failed to update installed_mods.json: {ex.Message}");
                }

                string summary = uninstalledFileNames.Count > 0
                    ? "Uninstalled files:\n- " + string.Join("\n- ", uninstalledFileNames)
                    : "No matching metadata entries were found to remove.";

                MessageBox.Show(summary, "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _viewModel.RefreshModList();
            }
        }



        private void SaveDownloadMetadata(int modId, string modName, ModFile file)
        {
            string metadataPath = Path.Combine(Settings.DefaultModsDir, "installed_mods.json");
            var entry = new InstalledModInfo
            {
                ModId = modId,
                ModName = modName,
                FileId = file.FileId,
                FileName = file.FileName,
                UploadedTimestamp = file.UploadedTimestamp
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
                    Console.WriteLine($"[WARN] Could not read existing metadata: {ex.Message}");
                }
            }

            list.RemoveAll(m => m.ModId == entry.ModId && m.FileName.Equals(entry.FileName, StringComparison.OrdinalIgnoreCase));
            list.Add(entry);

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(metadataPath, JsonSerializer.Serialize(list, options));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write metadata: {ex.Message}");
            }
        }

    }
}
