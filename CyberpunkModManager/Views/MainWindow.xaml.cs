using System.Windows;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;

namespace CyberpunkModManager.Views
{
    public partial class MainWindow : Window
    {
        private readonly Settings _settings;
        private readonly SettingsView _settingsView;
        private readonly ModListView _modListView;
        private readonly FilesView _filesView; // ✅ New FilesView field

        public MainWindow()
        {
            InitializeComponent();

            // Load settings once
            _settings = SettingsService.LoadSettings();

            // Pre-warn user if no API key is found
            if (string.IsNullOrWhiteSpace(_settings.NexusApiKey))
            {
                MessageBox.Show("Please enter your Nexus Mods API key in the Settings tab.", "API Key Missing");
            }

            // Create views early (this will trigger loading + constructor logic)
            _settingsView = new SettingsView();
            _modListView = new ModListView();
            _filesView = new FilesView(); // ✅ Initialize FilesView

            // Set them into the content controls so they're ready immediately
            SettingsTabContent.Content = _settingsView;
            ModsTabContent.Content = _modListView;
            FilesTabContent.Content = _filesView; // ✅ Assign FilesView
        }
    }
}
