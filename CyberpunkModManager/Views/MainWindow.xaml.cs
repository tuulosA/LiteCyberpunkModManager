using System.Windows;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;
using System.Windows.Media;

namespace CyberpunkModManager.Views
{
    public partial class MainWindow : Window
    {
        private readonly Settings _settings;
        private readonly SettingsView _settingsView;
        private readonly ModListView _modListView;
        private readonly FilesView _filesView;

        public MainWindow()
        {
            InitializeComponent();

            // Load settings
            _settings = SettingsService.LoadSettings();

            // Set dark background if using Dark theme
            if (Application.Current.Resources["WindowBackgroundBrush"] is Brush brush)
            {
                Background = brush;
            }
            else
            {
                Background = SystemColors.WindowBrush;
            }


            // Warn if API key is missing
            if (string.IsNullOrWhiteSpace(_settings.NexusApiKey))
            {
                MessageBox.Show("Please enter your Nexus Mods API key in the Settings tab.", "API Key Missing");
            }

            // Create views
            _settingsView = new SettingsView();
            _modListView = new ModListView();
            _filesView = new FilesView();

            // Assign views
            SettingsTabContent.Content = _settingsView;
            ModsTabContent.Content = _modListView;
            FilesTabContent.Content = _filesView;
        }
    }
}
