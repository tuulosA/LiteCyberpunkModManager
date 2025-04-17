using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.Models;

namespace CyberpunkModManager.Views
{
    public partial class DownloadFileWindow : Window
    {
        public List<int> SelectedFileIds { get; private set; } = new();

        private List<ModFile> _files;

        public DownloadFileWindow(List<ModFile> files)
        {
            InitializeComponent();
            _files = files;
            PopulateFileList();
        }

        private void PopulateFileList()
        {
            foreach (var file in _files)
            {
                double sizeInMb = file.FileSizeBytes / 1024.0 / 1024.0;
                string sizeLabel = sizeInMb >= 1
                    ? $"{sizeInMb:F2} MB"
                    : $"{file.FileSizeBytes / 1024.0:F2} KB";

                var text = $"{file.FileName}\nSize: {sizeLabel}\nUploaded: {file.UploadedTimestamp:G}\n{file.Description}";

                var checkbox = new CheckBox
                {
                    Content = new TextBlock
                    {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap,
                        Width = 550
                    },
                    Tag = file.FileId,
                    Margin = new Thickness(5)
                };

                FilesPanel.Children.Add(checkbox);
            }
        }

        private void DownloadSelected_Click(object sender, RoutedEventArgs e)
        {
            SelectedFileIds.Clear();
            foreach (var child in FilesPanel.Children)
            {
                if (child is CheckBox checkbox && checkbox.IsChecked == true && checkbox.Tag is int fileId)
                {
                    SelectedFileIds.Add(fileId);
                }
            }

            if (SelectedFileIds.Count == 0)
            {
                MessageBox.Show("Please select at least one file.", "No Files Selected", MessageBoxButton.OK, MessageBoxImage.Information);
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