using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.Models;
using System.IO;
using CyberpunkModManager.Services;
using System.Text.Json;
using System.Net.Http;
using CyberpunkModManager.ViewModels;

namespace CyberpunkModManager.Views
{
    public partial class DownloadFileWindow : Window
    {
        public List<int> SelectedFileIds { get; private set; } = new();

        private readonly List<ModFile> _files;
        private readonly List<InstalledModInfo> _downloadedMetadata;
        private readonly Dictionary<CheckBox, string> _checkboxFileNames = new();
        private readonly HashSet<string> _selectedFileNames = new();
        private readonly int _modId;
        private readonly string _modName; // ✅ Store actual mod name
        private readonly ModListViewModel _viewModel; // 💡 Reference to update mod status


        public DownloadFileWindow(List<ModFile> files, List<InstalledModInfo> downloadedFiles, int modId, string modName, ModListViewModel viewModel)
        {
            InitializeComponent();
            _files = files;
            _downloadedMetadata = downloadedFiles;
            _modId = modId;
            _modName = modName;
            _viewModel = viewModel; // 💡 Save reference to update status after download
            PopulateFileList();
        }


        public async Task SetProgressAsync(double percentage)
        {
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = percentage;

            await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void DownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            SelectedFileIds.Clear();
            foreach (var child in FilesPanel.Children)
            {
                if (child is CheckBox checkbox && checkbox.IsChecked == true && checkbox.Tag is int fileId)
                {
                    SelectedFileIds.Add(fileId);
                }
            }

            if (SelectedFileIds.Count == 0)
            {
                MessageBox.Show("Please select at least one file.", "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;

            var settings = SettingsService.LoadSettings();
            var api = new NexusApiService(settings.NexusApiKey);
            int total = SelectedFileIds.Count;
            bool anySuccess = false;

            for (int i = 0; i < total; i++)
            {
                int fileId = SelectedFileIds[i];
                var file = _files.FirstOrDefault(f => f.FileId == fileId);
                if (file == null) continue;

                var downloadUrl = await api.GetDownloadLinkAsync("cyberpunk2077", _modId, fileId);
                if (downloadUrl == null) continue;

                string sanitizedModFolder = PathUtils.SanitizeModName(_modName); // ✅ use actual mod name for folder
                string modFolderPath = Path.Combine(Settings.DefaultModsDir, sanitizedModFolder);
                Directory.CreateDirectory(modFolderPath);

                string baseName = Path.GetFileNameWithoutExtension(file.FileName);
                string savePath = Path.Combine(modFolderPath, baseName + ".zip");

                var progress = new Progress<double>(async percent =>
                {
                    double overallProgress = ((i + percent / 100.0) / total) * 100;
                    await SetProgressAsync(overallProgress);
                });

                bool success = await api.DownloadFileAsync(downloadUrl, savePath, progress);
                if (success)
                {
                    SaveDownloadMetadata(_modId, _modName, file); // ✅ Use correct mod name
                    anySuccess = true;
                }
            }

            DownloadProgressBar.Visibility = Visibility.Collapsed;

            if (anySuccess)
            {
                await _viewModel.UpdateModStatusAsync(_modId); // ✅ Refresh actual status
            }

            DialogResult = anySuccess;
            Close();
        }

        private void SaveDownloadMetadata(int modId, string modName, ModFile file)
        {
            string metadataPath = Path.Combine(Settings.DefaultModsDir, "installed_mods.json");
            var entry = new InstalledModInfo
            {
                ModId = modId,
                ModName = modName, // ✅ Correct mod name stored
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

            list.RemoveAll(m => m.ModId == modId && m.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));
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

        private void PopulateFileList()
        {
            var sortedFiles = _files.OrderByDescending(f => f.UploadedTimestamp).ToList();

            foreach (var file in sortedFiles)
            {
                double sizeInMb = file.FileSizeBytes / 1024.0 / 1024.0;
                string sizeLabel = sizeInMb >= 1
                    ? $"{sizeInMb:F2} MB"
                    : $"{file.FileSizeBytes / 1024.0:F2} KB";

                var text = $"{file.FileName}\nSize: {sizeLabel}\nUploaded: {file.UploadedTimestamp:G}\n{file.Description}";

                var checkbox = new CheckBox
                {
                    Content = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        Width = 550
                    },
                    Tag = file.FileId,
                    Margin = new Thickness(5)
                };

                string fullName = file.FileName;
                _checkboxFileNames[checkbox] = fullName;

                bool isAlreadyDownloaded = _downloadedMetadata.Any(d =>
                    d.ModId == _modId &&
                    d.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase) &&
                    d.UploadedTimestamp == file.UploadedTimestamp);

                if (isAlreadyDownloaded)
                {
                    checkbox.IsEnabled = false;
                    checkbox.ToolTip = "Already downloaded (same version)";
                }
                else
                {
                    checkbox.Checked += Checkbox_Checked;
                    checkbox.Unchecked += Checkbox_Unchecked;
                }

                FilesPanel.Children.Add(checkbox);
            }
        }

        private void Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox && _checkboxFileNames.TryGetValue(checkbox, out var fullName))
            {
                if (_selectedFileNames.Contains(fullName))
                {
                    MessageBox.Show($"You’ve already selected another file with the name '{fullName}'.", "Duplicate File Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    checkbox.IsChecked = false;
                }
                else
                {
                    _selectedFileNames.Add(fullName);
                }
            }
        }

        private void Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox && _checkboxFileNames.TryGetValue(checkbox, out var fullName))
            {
                _selectedFileNames.Remove(fullName);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}