using System.Windows;
using System.Windows.Controls;
using LiteCyberpunkModManager.Models;
using System.IO;
using LiteCyberpunkModManager.Services;
using System.Text.Json;
using LiteCyberpunkModManager.ViewModels;
using System.Windows.Media;
using LiteCyberpunkModManager.Helpers;
using System.Diagnostics;

namespace LiteCyberpunkModManager.Views
{
    public partial class DownloadFileWindow : Window
    {
        public List<int> SelectedFileIds { get; private set; } = new();

        private readonly List<ModFile> _files;
        private readonly List<InstalledModInfo> _downloadedMetadata;
        private readonly Dictionary<CheckBox, string> _checkboxFileNames = new();
        private readonly HashSet<string> _selectedFileNames = new();
        private readonly int _modId;
        private readonly string _modName; 
        private readonly ModListViewModel _viewModel; 


        public DownloadFileWindow(List<ModFile> files, List<InstalledModInfo> downloadedFiles, int modId, string modName, ModListViewModel viewModel)
        {
            ThemeHelper.ApplyThemeTo(this); 
            InitializeComponent();

            _files = files;
            _downloadedMetadata = downloadedFiles;
            _modId = modId;
            _modName = modName;
            _viewModel = viewModel;

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
            api.NotificationRaised += n =>
            {
                var icon = n.Type switch
                {
                    NotificationType.Error => MessageBoxImage.Error,
                    NotificationType.Warning => MessageBoxImage.Warning,
                    _ => MessageBoxImage.Information
                };
                MessageBox.Show(n.Message, n.Title, MessageBoxButton.OK, icon);
            };
            int total = SelectedFileIds.Count;
            bool anySuccess = false;

            for (int i = 0; i < total; i++)
            {
                int fileId = SelectedFileIds[i];
                var file = _files.FirstOrDefault(f => f.FileId == fileId);
                if (file == null) continue;

                var downloadUrl = await api.GetDownloadLinkAsync("cyberpunk2077", _modId, fileId);
                if (downloadUrl == null) continue;

                string sanitizedModFolder = PathUtils.SanitizeModName(_modName);
                string modFolderPath = Path.Combine(settings.OutputDir, sanitizedModFolder);
                Directory.CreateDirectory(modFolderPath);

                string savePath = Path.Combine(modFolderPath, file.FileName);

                var progress = new Progress<double>(async percent =>
                {
                    double overallProgress = ((i + percent / 100.0) / total) * 100;
                    await SetProgressAsync(overallProgress);
                });

                bool success = await api.DownloadFileAsync(downloadUrl, savePath, progress);
                if (success)
                {
                    SaveDownloadMetadata(_modId, _modName, file);
                    anySuccess = true;
                }
            }

            DownloadProgressBar.Visibility = Visibility.Collapsed;

            if (anySuccess)
            {
                await _viewModel.UpdateModStatusAsync(_modId);
            }

            // look for FilesView and tell it to refresh
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                if (mainWindow.FindName("FilesTabContent") is ContentControl filesTab &&
                    filesTab.Content is FilesView filesView)
                {
                    filesView.RefreshFileList();
                }
            }

            DialogResult = anySuccess;
            Close();
        }


        private void SaveDownloadMetadata(int modId, string modName, ModFile file)
        {
            string metadataPath = PathConfig.DownloadedMods;
            Directory.CreateDirectory(PathConfig.AppDataRoot);
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
                    Debug.WriteLine($"[WARN] Could not read existing metadata: {ex.Message}");
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
                Debug.WriteLine($"[ERROR] Failed to write metadata: {ex.Message}");
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

                string formattedDescription = TextFormatter.ConvertHtmlToPlainText(file.Description);
                string fullText = $"{file.FileName}\nSize: {sizeLabel}\nUploaded: {file.UploadedTimestamp:G}\n{formattedDescription}";

                var textBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Width = 550,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    Background = Brushes.Transparent
                };

                // Add parsed inlines one by one
                foreach (var inline in TextFormatter.ParseToInlines(fullText))
                {
                    textBlock.Inlines.Add(inline);
                }

                var checkbox = new CheckBox
                {
                    Content = textBlock,
                    Tag = file.FileId,
                    Margin = new Thickness(5),
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    Background = (Brush)Application.Current.Resources["ControlBackgroundBrush"]
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
            if (sender is not CheckBox newlyChecked || !_checkboxFileNames.TryGetValue(newlyChecked, out var fullName)) return;

            string baseName = Path.GetFileNameWithoutExtension(fullName);

            // check if the baseName is already selected
            if (_selectedFileNames.Contains(baseName))
            {
                // find and uncheck the previously selected checkbox with the same base name
                var previousCheckbox = _checkboxFileNames
                    .Where(kv => Path.GetFileNameWithoutExtension(kv.Value) == baseName && kv.Key.IsChecked == true && kv.Key != newlyChecked)
                    .Select(kv => kv.Key)
                    .FirstOrDefault();

                if (previousCheckbox != null)
                {
                    previousCheckbox.Checked -= Checkbox_Checked;
                    previousCheckbox.IsChecked = false;
                    previousCheckbox.Checked += Checkbox_Checked;

                    _selectedFileNames.Remove(baseName);
                }
            }

            _selectedFileNames.Add(baseName);
        }


        private void Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox && _checkboxFileNames.TryGetValue(checkbox, out var fullName))
            {
                string baseName = Path.GetFileNameWithoutExtension(fullName);
                _selectedFileNames.Remove(baseName);
            }
        }



        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
