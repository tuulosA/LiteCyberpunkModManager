using System.Windows;

namespace LiteCyberpunkModManager.Views
{
    public partial class ModlistTrackingWindow : Window
    {
        public ModlistTrackingWindow()
        {
            InitializeComponent();
        }

        public void SetStatus(string message)
        {
            Dispatcher.Invoke(() => StatusText.Text = message);
        }

        public void ReportProgress(double percent)
        {
            Dispatcher.Invoke(() => ProgressBar.Value = percent);
        }
    }
}