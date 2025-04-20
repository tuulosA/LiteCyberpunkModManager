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

        private async void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = DownloadedFilesGrid.SelectedItems.Cast<InstalledModDisplay>().ToList();
            if (selectedMods.Count == 0)
            {
                MessageBox.Show("Select at least one mod to install.");
                return;
            }

            var progressWindow = new ProgressWindow(selectedMods.Count);
            progressWindow.Owner = Window.GetWindow(this);
            progressWindow.Show();

            await Task.Run(() =>
            {
                int current = 0;

                foreach (var mod in selectedMods)
                {
                    string folderName = PathUtils.SanitizeModName(mod.ModName);
                    string zipPath = Path.Combine(Settings.DefaultModsDir, folderName, Path.GetFileNameWithoutExtension(mod.FileName) + ".zip");

                    if (!File.Exists(zipPath)) continue;

                    bool success = ModInstallerService.InstallModFile(
                        zipPath,
                        mod.ModName,
                        mod.FileName,
                        out _,
                        (percent, file) =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressWindow.SetSubProgress(percent, file);
                            });
                        });

                    current++;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (success) mod.Status = "Installed";
                        progressWindow.UpdateProgress(current, mod.ModName);
                    });
                }
            });

            progressWindow.Close();
            DownloadedFilesGrid.Items.Refresh();
            MessageBox.Show("Selected mods installed.", "Install Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (ModInstallerService.UninstallMod(mod.ModName, mod.FileName))
                {
                    mod.Status = "Not Installed";
                }

            }

            DownloadedFilesGrid.Items.Refresh();
            MessageBox.Show("Selected mods uninstalled.", "Uninstall Complete", MessageBoxButton.OK, MessageBoxImage.Information);
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
