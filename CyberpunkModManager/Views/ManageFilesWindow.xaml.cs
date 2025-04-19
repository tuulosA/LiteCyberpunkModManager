using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CyberpunkModManager.Views
{
    public partial class ManageFilesWindow : Window
    {
        public List<string> SelectedFiles { get; private set; } = new();

        public ManageFilesWindow(List<string> installedFilePaths)
        {
            InitializeComponent();
            foreach (var path in installedFilePaths)
            {
                var checkBox = new CheckBox
                {
                    Content = Path.GetFileName(path),
                    Tag = path,
                    Margin = new Thickness(5)
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
