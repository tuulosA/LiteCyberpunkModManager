using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiteCyberpunkModManager.Helpers;

namespace LiteCyberpunkModManager.Views
{
    public partial class ManageFilesWindow : Window
    {
        public List<string> SelectedFiles { get; private set; } = new();

        public ManageFilesWindow(List<string> installedFilePaths)
        {
            ThemeHelper.ApplyThemeTo(this);
            InitializeComponent();

            foreach (var path in installedFilePaths)
            {
                var checkBox = new CheckBox
                {
                    Content = new TextBlock
                    {
                        Text = Path.GetFileName(path),
                        Foreground = (Brush)Application.Current.Resources["TextBrush"],
                        Background = Brushes.Transparent,
                        TextWrapping = TextWrapping.Wrap
                    },
                    Tag = path,
                    Margin = new Thickness(5),
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    Background = (Brush)Application.Current.Resources["ControlBackgroundBrush"]
                };

                FilesPanel.Children.Add(checkBox);
            }
        }


        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in FilesPanel.Children)
            {
                if (child is CheckBox check && check.IsChecked == true)
                {
                    SelectedFiles.Add(check.Tag as string ?? "");
                }
            }

            if (SelectedFiles.Count == 0)
            {
                MessageBox.Show("Select at least one file to delete.");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in FilesPanel.Children)
            {
                if (child is CheckBox check)
                {
                    check.IsChecked = true;
                }
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in FilesPanel.Children)
            {
                if (child is CheckBox check)
                {
                    check.IsChecked = false;
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
