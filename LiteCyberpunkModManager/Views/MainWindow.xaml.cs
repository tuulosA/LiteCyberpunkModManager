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
        private readonly Bg3LoadOrderView _bg3LoadOrderView;

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
            _bg3LoadOrderView = new Bg3LoadOrderView();

            SettingsTabContent.Content = _settingsView;
            ModsTabContent.Content = _modListView;
            FilesTabContent.Content = _filesView;
            Bg3LoadOrderTabContent.Content = _bg3LoadOrderView;

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

            // Show BG3 load-order tab only when BG3 is active
            if (Bg3LoadOrderTab != null)
            {
                Bg3LoadOrderTab.Visibility = currentSettings.SelectedGame == GameId.BaldursGate3
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            _bg3LoadOrderView.RefreshForCurrentGame();
        }
    }
}


