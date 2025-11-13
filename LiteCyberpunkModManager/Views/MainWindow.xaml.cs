using System;
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

            // First-run prompt: choose game if not set explicitly (optional)
            if (!Enum.IsDefined(typeof(GameId), _settings.SelectedGame))
            {
                _settings.SelectedGame = GameId.Cyberpunk2077;
                SettingsService.SaveSettings(_settings);
            }

            _settingsView = new SettingsView();
            _modListView = new ModListView();
            _filesView = new FilesView();

            SettingsTabContent.Content = _settingsView;
            ModsTabContent.Content = _modListView;
            FilesTabContent.Content = _filesView;

            UpdateActiveGameLabel();
        }

        public void UpdateActiveGameLabel()
        {
            var currentSettings = SettingsService.LoadSettings();

            string display = currentSettings.SelectedGame switch
            {
                GameId.BaldursGate3 => "Baldur's Gate 3",
                _ => "Cyberpunk 2077"
            };

            ActiveGameLabel.Text = display;
        }
    }
}


