using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;

namespace LiteCyberpunkModManager.Views
{
    public partial class Bg3LoadOrderView : UserControl
    {
        private readonly ObservableCollection<Bg3ModuleEntry> _modules = new();

        public Bg3LoadOrderView()
        {
            InitializeComponent();
            Loaded += Bg3LoadOrderView_Loaded;
            ModulesGrid.ItemsSource = _modules;
        }

        private void Bg3LoadOrderView_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshForCurrentGame();
        }

        public void RefreshForCurrentGame()
        {
            var settings = SettingsService.LoadSettings();
            if (settings.SelectedGame != GameId.BaldursGate3)
            {
                HintText.Text = "Select Baldur's Gate 3 in Settings to edit load order.";
                ModulesGrid.ItemsSource = null;
                _modules.Clear();
                return;
            }

            HintText.Text = "Drag or use the buttons below to adjust load order. Top entries load first.";
            ModulesGrid.ItemsSource = _modules;

            _modules.Clear();
            foreach (var m in Bg3ModSettingsService.LoadModules())
            {
                _modules.Add(m);
            }

            // Set row headers as 1-based indices
            ModulesGrid.LoadingRow += (s, e) => e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ModulesGrid.SelectedItem is not Bg3ModuleEntry selected) return;
            int index = _modules.IndexOf(selected);
            if (index <= 0) return;
            _modules.Move(index, index - 1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ModulesGrid.SelectedItem is not Bg3ModuleEntry selected) return;
            int index = _modules.IndexOf(selected);
            if (index < 0 || index >= _modules.Count - 1) return;
            _modules.Move(index, index + 1);
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            RefreshForCurrentGame();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.LoadSettings();
            if (settings.SelectedGame != GameId.BaldursGate3)
            {
                MessageBox.Show("Load order editing is only available for Baldur's Gate 3.", "BG3 Only",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Bg3ModSettingsService.SaveOrder(_modules.ToList());
            MessageBox.Show("BG3 load order saved.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

