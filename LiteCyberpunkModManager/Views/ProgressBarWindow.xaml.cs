using System.Windows;

namespace LiteCyberpunkModManager.Views
{
    public partial class ProgressBarWindow : Window
    {
        public ProgressBarWindow()
        {
            InitializeComponent();
        }

        public void SetProgress(double value)
        {
            ProgressBarControl.Value = value;
            StatusText.Text = $"Downloading... {value:F1}%";
        }
    }
}
