using System.Windows;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;
using System.Windows.Media;

namespace LiteCyberpunkModManager.Views
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

            _settings = SettingsService.LoadSettings();

            if (Application.Current.Resources["WindowBackgroundBrush"] is Brush brush)
            {
                Background = brush;
            }
            else
            {
                Background = SystemColors.WindowBrush;
            }

            if (string.IsNullOrWhiteSpace(_settings.NexusApiKey))
            {
                MessageBox.Show("Please enter your Nexus Mods API key in the Settings tab.", "API Key Missing");
            }

            _settingsView = new SettingsView();
            _modListView = new ModListView();
            _filesView = new FilesView();

            SettingsTabContent.Content = _settingsView;
            ModsTabContent.Content = _modListView;
            FilesTabContent.Content = _filesView;
        }
    }
}
