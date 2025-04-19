using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;

namespace CyberpunkModManager.Views
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

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.NexusApiKey = ApiKeyBox.Password;

            Debug.WriteLine("[SettingsView] Saving settings...");
            SettingsService.SaveSettings(_settings);

            ApplyTheme(_settings.AppTheme);
        }

        private void ApplyTheme(AppTheme theme)
        {
            var appResources = Application.Current.Resources.MergedDictionaries;

            // Remove existing themes
            var existingThemes = appResources.Where(dict =>
                dict.Source != null &&
                (dict.Source.OriginalString.Contains("DarkTheme.xaml") || dict.Source.OriginalString.Contains("LightTheme.xaml"))
            ).ToList();

            foreach (var dict in existingThemes)
                appResources.Remove(dict);

            // Apply new theme
            var themeDict = new ResourceDictionary();
            if (theme == AppTheme.Dark)
            {
                themeDict.Source = new Uri("/CyberpunkModManager;component/Resources/DarkTheme.xaml", UriKind.Relative);
                Debug.WriteLine("[SettingsView] Applied Dark theme.");
            }
            else
            {
                themeDict.Source = new Uri("/CyberpunkModManager;component/Resources/LightTheme.xaml", UriKind.Relative);
                Debug.WriteLine("[SettingsView] Applied Light theme.");
            }

            appResources.Add(themeDict);

            // Update the main window background live
            if (Application.Current.MainWindow is Window mainWindow)
            {
                var brush = Application.Current.Resources["WindowBackgroundBrush"] as Brush;

                // Fallback to default if not found (like in light theme)
                mainWindow.Background = brush ?? SystemColors.WindowBrush;
            }

        }





        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _settings.NexusApiKey = ApiKeyBox.Password;
        }
    }
}
