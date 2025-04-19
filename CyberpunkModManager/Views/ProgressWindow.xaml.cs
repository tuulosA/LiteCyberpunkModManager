using System.Windows;

namespace CyberpunkModManager.Views
{
    public partial class ProgressWindow : Window
    {
        private int _total;

        public ProgressWindow(int totalMods)
        {
            InitializeComponent();
            _total = totalMods;
            this.ProgressBar.Maximum = totalMods;
        }

        public void UpdateProgress(int current, string currentMod)
        {
            this.ProgressBar.Value = current;
            this.StatusText.Text = $"Installing: {currentMod} ({current}/{_total})";
        }

        public void SetSubProgress(int percent, string fileName)
        {
            this.SubProgressBar.Value = percent;
            this.SubStatusText.Text = $"Extracting: {fileName} ({percent}%)";
        }
    }
}
