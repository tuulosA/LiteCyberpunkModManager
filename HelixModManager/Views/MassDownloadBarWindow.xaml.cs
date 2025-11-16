using System.Windows;

namespace HelixModManager.Views
{
    public partial class MassDownloadBarWindow : Window
    {
        public MassDownloadBarWindow()
        {
            InitializeComponent();
        }

        public void SetModName(string modName)
        {
            ModNameText.Text = $"Mod: {modName}";
        }

        public void SetFileName(string fileName)
        {
            FileNameText.Text = $"File: {fileName}";
        }

        public void SetFileCounter(int current, int total)
        {
            FileCounterText.Text = $"File {current} of {total}";
        }

        public void SetStatusText(string status)
        {
            StatusText.Text = status;
        }

        public void SetOverallProgress(double percent)
        {
            ProgressBarControl.Value = percent;
        }

        public void SetFileProgress(double percent)
        {
            FileProgressBar.Value = percent;
        }
    }
}

