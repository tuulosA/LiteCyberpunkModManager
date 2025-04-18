using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.Models;
using System.IO;

namespace CyberpunkModManager.Views
{
    public partial class DownloadFileWindow : Window
    {
        public List<int> SelectedFileIds { get; private set; } = new();

        private readonly List<ModFile> _files;
        private readonly List<InstalledModInfo> _downloadedMetadata;
        private readonly Dictionary<CheckBox, string> _checkboxFileNames = new();
        private readonly HashSet<string> _selectedFileNames = new();
        private readonly int _modId;

        public DownloadFileWindow(List<ModFile> files, List<InstalledModInfo> downloadedFiles, int modId)
        {
            InitializeComponent();
            _files = files;
            _downloadedMetadata = downloadedFiles;
            _modId = modId;
            PopulateFileList();
        }

        private void PopulateFileList()
        {
            var sortedFiles = _files.OrderByDescending(f => f.UploadedTimestamp).ToList();

            foreach (var file in sortedFiles)
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

                string fullName = file.FileName;
                _checkboxFileNames[checkbox] = fullName;

                bool isAlreadyDownloaded = _downloadedMetadata.Any(d =>
                    d.ModId == _modId &&
                    d.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase) &&
                    d.UploadedTimestamp == file.UploadedTimestamp);

                if (isAlreadyDownloaded)
                {
                    checkbox.IsEnabled = false;
                    checkbox.ToolTip = "Already downloaded (same version)";
                }
                else
                {
                    checkbox.Checked += Checkbox_Checked;
                    checkbox.Unchecked += Checkbox_Unchecked;
                }

                FilesPanel.Children.Add(checkbox);
            }
        }

        private void Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox && _checkboxFileNames.TryGetValue(checkbox, out var fullName))
            {
                if (_selectedFileNames.Contains(fullName))
                {
                    MessageBox.Show($"You’ve already selected another file with the name '{fullName}'.", "Duplicate File Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    checkbox.IsChecked = false;
                }
                else
                {
                    _selectedFileNames.Add(fullName);
                }
            }
        }

        private void Checkbox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox && _checkboxFileNames.TryGetValue(checkbox, out var fullName))
            {
                _selectedFileNames.Remove(fullName);
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
