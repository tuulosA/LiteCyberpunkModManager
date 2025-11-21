using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Settings _settings;
        private readonly NexusSsoService _ssoService = new();
        private CancellationTokenSource? _ssoLinkCancellation;

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
            UpdateSsoStatusText();
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[SettingsView] Saving settings...");
            SettingsService.SaveSettings(_settings);
            ApplyTheme(_settings.AppTheme);

            MessageBox.Show("Settings saved successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);

            await RefreshViewsAsync();
        }

        private async Task RefreshViewsAsync()
        {
            if (Application.Current.MainWindow is not MainWindow mainWindow)
                return;

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

        private async void LinkNexusAccount_Click(object sender, RoutedEventArgs e)
        {
            if (_ssoLinkCancellation != null)
                return;

            if (sender is not Button button)
                return;

            button.IsEnabled = false;
            var progress = new Progress<string>(message => SsoStatusText.Text = message);
            _ssoLinkCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            try
            {
                var result = await _ssoService.LinkAccountAsync(_settings, progress, _ssoLinkCancellation.Token);
                if (result.Success && !string.IsNullOrWhiteSpace(result.ApiKey))
                {
                    _settings.NexusApiKey = result.ApiKey;
                    UpdateSsoStatusText();
                    MessageBox.Show("Successfully linked your Nexus Mods account.", "Linked", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshViewsAsync();
                }
                else if (result.IsCancelled)
                {
                    var message = result.ErrorMessage ?? "SSO linking cancelled.";
                    SsoStatusText.Text = message;
                    UpdateUnlinkButtonState();
                }
                else
                {
                    var message = result.ErrorMessage ?? "Unable to link your Nexus Mods account.";
                    SsoStatusText.Text = message;
                    UpdateUnlinkButtonState();
                    MessageBox.Show(message, "Link Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                var message = $"Failed to link Nexus Mods account.{Environment.NewLine}{ex.Message}";
                SsoStatusText.Text = "Link failed.";
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.IsEnabled = true;
                _ssoLinkCancellation?.Dispose();
                _ssoLinkCancellation = null;

                if (string.IsNullOrWhiteSpace(_settings.NexusApiKey))
                {
                    UpdateSsoStatusText();
                }
            }
        }

        private async void UnlinkNexusAccount_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.NexusApiKey))
                return;

            var confirm = MessageBox.Show(
                "Unlinking will remove your stored Nexus Mods API key. Continue?",
                "Unlink Account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            _settings.NexusApiKey = string.Empty;
            _settings.NexusSsoLinkedAt = null;
            _settings.NexusSsoRequestId = null;
            _settings.NexusSsoConnectionToken = null;
            SettingsService.SaveSettings(_settings);
            UpdateSsoStatusText();
            MessageBox.Show("Your Nexus Mods account has been unlinked.", "Unlinked", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshViewsAsync();
        }

        private void UpdateSsoStatusText()
        {
            if (!string.IsNullOrWhiteSpace(_settings.NexusApiKey))
            {
                if (_settings.NexusSsoLinkedAt.HasValue)
                {
                    var timestamp = _settings.NexusSsoLinkedAt.Value.ToLocalTime().ToString("g");
                    SsoStatusText.Text = $"Linked ({timestamp})";
                }
                else
                {
                    SsoStatusText.Text = "Linked";
                }
            }
            else
            {
                SsoStatusText.Text = "Not linked";
            }

            UpdateUnlinkButtonState();
        }

        private void UpdateUnlinkButtonState()
        {
            if (UnlinkNexusAccountButton != null)
            {
                UnlinkNexusAccountButton.IsEnabled = !string.IsNullOrWhiteSpace(_settings.NexusApiKey);
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






