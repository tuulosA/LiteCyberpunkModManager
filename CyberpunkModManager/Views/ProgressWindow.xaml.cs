using System.Windows;
using System.Windows.Controls;

namespace CyberpunkModManager.Views
{
    public partial class ProgressWindow : Window
    {
        private int _total;

        public ProgressWindow(int totalMods)
        {
            InitializeComponent();
            _total = totalMods;
            this.ProgressBar.Maximum = totalMods; // Add this.
        }

        public void UpdateProgress(int current, string currentMod)
        {
            this.ProgressBar.Value = current;
            this.StatusText.Text = $"Installing: {currentMod} ({current}/{_total})";
        }
    }
}
