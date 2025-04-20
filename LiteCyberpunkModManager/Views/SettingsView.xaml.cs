using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;

namespace LiteCyberpunkModManager.Views
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

            if (Application.Current.MainWindow is MainWindow mainWindow &&
                mainWindow.FindName("ModsTabContent") is ContentControl modsTab &&
                modsTab.Content is ModListView modListView)
            {
                modListView.ReinitializeApiService();
                await modListView.FetchModsFromApiAsync();
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
                themeDict.Source = new Uri("/LiteCPMM;component/Resources/DarkTheme.xaml", UriKind.Relative);
                Debug.WriteLine("[SettingsView] Applied Dark theme.");
            }
            else
            {
                themeDict.Source = new Uri("/LiteCPMM;component/Resources/LightTheme.xaml", UriKind.Relative);
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
    }
}
