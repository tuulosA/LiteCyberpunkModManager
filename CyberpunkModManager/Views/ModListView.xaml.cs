using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;
using CyberpunkModManager.ViewModels;
using CyberpunkModManager.Views;
using System.IO;

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

            var dialog = new DownloadFileWindow(files);
            var result = dialog.ShowDialog();

            if (result == true && dialog.SelectedFileIds.Count > 0)
            {
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
                        MessageBox.Show($"Downloaded '{file.FileName}' to Mods directory.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Failed to download '{file.FileName}'.", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

    }
}
