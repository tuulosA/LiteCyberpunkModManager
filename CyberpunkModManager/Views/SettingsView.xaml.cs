using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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

            // Load settings from service
            _settings = SettingsService.LoadSettings();

            Debug.WriteLine($"[SettingsView] Loaded settings:");
            Debug.WriteLine($"    OutputDir: {_settings.OutputDir}");
            Debug.WriteLine($"    GameInstallationDir: {_settings.GameInstallationDir}");
            Debug.WriteLine($"    NexusApiKey: {_settings.NexusApiKey}");

            // Set data context for binding
            DataContext = _settings;

            // Sync API key manually for the password box
            ApiKeyBox.Password = _settings.NexusApiKey;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.NexusApiKey = ApiKeyBox.Password;

            Debug.WriteLine("[SettingsView] Saving settings...");
            Debug.WriteLine($"    OutputDir: {_settings.OutputDir}");
            Debug.WriteLine($"    GameInstallationDir: {_settings.GameInstallationDir}");
            Debug.WriteLine($"    NexusApiKey: {_settings.NexusApiKey}");

            SettingsService.SaveSettings(_settings);
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _settings.NexusApiKey = ApiKeyBox.Password;
        }
    }
}
