using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HelixModManager.Helpers;
using HelixModManager.Models;
using HelixModManager.Services;
using HelixModManager.ViewModels;

namespace HelixModManager.Views
{
    public partial class SettingsView : UserControl
    {
        private Settings _settings;

        public SettingsView()
        {
            Debug.WriteLine("[SettingsView] Constructor fired.");

            InitializeComponent();

            _settings = SettingsService.LoadSettings();

            Debug.WriteLine($"[SettingsView] Loaded settings:");
            Debug.WriteLine($"    OutputDir: {_settings.OutputDir}");
            Debug.WriteLine($"    GameInstallationDir: {_settings.GameInstallationDir}");
            Debug.WriteLine($"    NexusApiKey: {_settings.NexusApiKey}");

            DataContext = _settings;
            ApiKeyBox.Password = _settings.NexusApiKey;
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.NexusApiKey = ApiKeyBox.Password;

            Debug.WriteLine("[SettingsView] Saving settings...");
            SettingsService.SaveSettings(_settings);
            ApplyTheme(_settings.AppTheme);

            MessageBox.Show("Settings saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);

            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                // Update header immediately so user sees the new game selection
                mainWindow.UpdateActiveGameLabel();

                if (mainWindow.FindName("ModsTabContent") is ContentControl modsTab &&
                    modsTab.Content is ModListView modListView)
                {
                    modListView.ReinitializeApiService();
                    await modListView.FetchModsFromApiAsync();
                }

                if (mainWindow.FindName("FilesTabContent") is ContentControl filesTab &&
                    filesTab.Content is FilesView filesView)
                {
                    filesView.RefreshFileList();
                }
            }
        }


        private void ApplyTheme(AppTheme theme)
        {
            var appResources = Application.Current.Resources.MergedDictionaries;

            // remove existing themes
            var existingThemes = appResources.Where(dict =>
                dict.Source != null &&
                (dict.Source.OriginalString.Contains("DarkTheme.xaml") || dict.Source.OriginalString.Contains("LightTheme.xaml"))
            ).ToList();

            foreach (var dict in existingThemes)
                appResources.Remove(dict);

            // apply new theme
            var themeDict = new ResourceDictionary();
            if (theme == AppTheme.Dark)
            {
                themeDict.Source = new Uri("/HMM;component/Resources/DarkTheme.xaml", UriKind.Relative);
                Debug.WriteLine("[SettingsView] Applied Dark theme.");
            }
            else
            {
                themeDict.Source = new Uri("/HMM;component/Resources/LightTheme.xaml", UriKind.Relative);
                Debug.WriteLine("[SettingsView] Applied Light theme.");
            }

            appResources.Add(themeDict);

            // update the main window background live
            if (Application.Current.MainWindow is Window mainWindow)
            {
                var brush = Application.Current.Resources["WindowBackgroundBrush"] as Brush;

                // fallback to default if not found
                mainWindow.Background = brush ?? SystemColors.WindowBrush;
            }

        }

        private void OpenOutputDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_settings.OutputDir))
                {
                    Process.Start("explorer.exe", _settings.OutputDir);
                }
                else
                {
                    MessageBox.Show("Output directory does not exist.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open output directory.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenGameDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_settings.GameInstallationDir))
                {
                    Process.Start("explorer.exe", _settings.GameInstallationDir);
                }
                else
                {
                    MessageBox.Show("Game installation directory does not exist.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open game directory.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _settings.NexusApiKey = ApiKeyBox.Password;
        }


        private void ExportModlist_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = "modlist.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var path = saveFileDialog.FileName;

                if (!File.Exists(PathConfig.DownloadedMods))
                {
                    MessageBox.Show("No modlist found to export. Download at least one mod first.", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(PathConfig.DownloadedMods, path, overwrite: true);
                    MessageBox.Show("Modlist exported successfully.", "Export Complete");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private async void ImportModlist_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            var filePath = openFileDialog.FileName;
            var json = File.ReadAllText(filePath);
            var importedMods = JsonSerializer.Deserialize<List<InstalledModInfo>>(json) ?? new();

            var api = new NexusApiService(SettingsService.LoadSettings().NexusApiKey);
            var modsToTrack = importedMods.DistinctBy(m => m.ModId).ToList();

            var importWindow = new ModlistTrackingWindow
            {
                Owner = Application.Current.MainWindow
            };
            importWindow.Show();

            int total = modsToTrack.Count;
            int completed = 0;
            int successCount = 0;

            var tasks = modsToTrack.Select(async mod =>
            {
                var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
                if (await api.TrackModAsync(mod.ModId, slug))
                    Interlocked.Increment(ref successCount);

                int done = Interlocked.Increment(ref completed);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    double percent = (double)done / total * 100;
                    importWindow.ReportProgress(percent);
                    importWindow.SetStatus($"Tracking mod {done} of {total}...");
                });
            });

            await Task.WhenAll(tasks);
            importWindow.Close();

            MessageBox.Show($"Successfully tracked {successCount} mod(s).", "Import Complete");

            // refresh mod list view if available
            if (Application.Current.MainWindow is MainWindow mainWindow &&
                mainWindow.FindName("ModsTabContent") is ContentControl modsTab &&
                modsTab.Content is ModListView modListView)
            {
                await modListView.FetchModsFromApiAsync();
            }
        }



        private async void ClearTrackedMods_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Are you sure you want to untrack all mods?",
                "Confirm",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            var doubleConfirm = MessageBox.Show(
                "This will untrack ALL currently followed mods. Are you REALLY sure?",
                "Final Confirmation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (doubleConfirm != MessageBoxResult.Yes) return;

            var api = new NexusApiService(SettingsService.LoadSettings().NexusApiKey);
            var slug2 = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
            var modIds = await api.GetTrackedModIdsAsync(slug2);

            if (modIds.Count == 0)
            {
                MessageBox.Show("No mods to untrack.", "Done");
                return;
            }

            var progressWindow = new ModlistTrackingWindow
            {
                Owner = Application.Current.MainWindow
            };
            progressWindow.SetStatus("Untracking mods...");
            progressWindow.Show();

            int total = modIds.Count;
            int completed = 0;
            int removedCount = 0;

            var tasks = modIds.Select(async modId =>
            {
                var slug3 = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
                if (await api.UntrackModAsync(modId, slug3))
                    Interlocked.Increment(ref removedCount);

                int done = Interlocked.Increment(ref completed);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    double percent = (double)done / total * 100;
                    progressWindow.SetStatus($"Untracking mod ID {modId} ({done}/{total})...");
                    progressWindow.ReportProgress(percent);
                });
            });

            await Task.WhenAll(tasks);
            progressWindow.Close();

            // remove untracked mods from cache
            var cachedMods = ModCacheService.LoadCachedMods();
            if (cachedMods != null)
            {
                cachedMods.RemoveAll(mod => modIds.Contains(mod.ModId));
                ModCacheService.SaveCachedMods(cachedMods);
                Debug.WriteLine($"[CACHE] Removed {removedCount} mods from mod_cache.json.");
            }

            MessageBox.Show($"Successfully untracked {removedCount} mod(s).", "Clear Complete");

            // refresh mod list view if available
            if (Application.Current.MainWindow is MainWindow mainWindow &&
                mainWindow.FindName("ModsTabContent") is ContentControl modsTab &&
                modsTab.Content is ModListView modListView)
            {
                await modListView.FetchModsFromApiAsync();
            }
        }



    }
}






