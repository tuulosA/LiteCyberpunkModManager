using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.ViewModels;
using CyberpunkModManager.Services;
using System.IO;
using System.Linq;
using CyberpunkModManager.Models;
using System.Diagnostics;

namespace CyberpunkModManager.Views
{
    public partial class FilesView : UserControl
    {
        private readonly FilesViewModel _viewModel;

        public FilesView()
        {
            InitializeComponent();
            _viewModel = new FilesViewModel();
            DataContext = _viewModel;
        }

        public void RefreshFileList()
        {
            _viewModel.Reload();
        }

        private void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = DownloadedFilesGrid.SelectedItems.Cast<InstalledModDisplay>().ToList();
            if (selectedMods.Count == 0)
            {
                MessageBox.Show("Select at least one mod to install.");
                return;
            }

            foreach (var mod in selectedMods)
            {
                string folderName = PathUtils.SanitizeModName(mod.ModName);
                string zipPath = Path.Combine(Settings.DefaultModsDir, folderName, Path.GetFileNameWithoutExtension(mod.FileName) + ".zip");

                if (!File.Exists(zipPath))
                {
                    MessageBox.Show($"Zip not found for {mod.FileName}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;
                }

                ModInstallerService.InstallModFile(zipPath, mod.ModName, mod.FileName, out _);
            }

            MessageBox.Show("Selected mods installed.", "Install Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshFileList();
        }

        private void UninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = DownloadedFilesGrid.SelectedItems.Cast<InstalledModDisplay>().ToList();
            if (selectedMods.Count == 0)
            {
                MessageBox.Show("Select at least one mod to uninstall.");
                return;
            }

            foreach (var mod in selectedMods)
            {
                ModInstallerService.UninstallMod(mod.ModName);
            }

            MessageBox.Show("Selected mods uninstalled.", "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshFileList();
        }


        private void ModName_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock tb)
            {
                var modDisplay = tb.DataContext as InstalledModDisplay;
                if (modDisplay == null) return;

                string folderName = modDisplay.ModName;
                string fullPath = Path.Combine(Settings.DefaultModsDir, folderName);

                if (Directory.Exists(fullPath))
                {
                    try
                    {
                        Process.Start("explorer.exe", fullPath);
                    }
                    catch
                    {
                        MessageBox.Show("Failed to open the mod folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("The mod folder was not found.", "Folder Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }



    }
}
