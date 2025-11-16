using System.Windows;
using System.Windows.Controls;
using LiteCyberpunkModManager.ViewModels;
using LiteCyberpunkModManager.Services;
using System.IO;
using LiteCyberpunkModManager.Models;
using System.Diagnostics;
using LiteCyberpunkModManager.Helpers;

namespace LiteCyberpunkModManager.Views
{
    public partial class FilesView : UserControl
    {
        private readonly FilesViewModel _viewModel;

        public FilesView()
        {
            InitializeComponent();
            _viewModel = new FilesViewModel();
            DataContext = _viewModel;

            App.GlobalFilesView = this; // set global reference

            // Subscribe to installer notifications and render via UI
            ModInstallerService.NotificationRaised += n =>
            {
                var icon = n.Type switch
                {
                    NotificationType.Error => MessageBoxImage.Error,
                    NotificationType.Warning => MessageBoxImage.Warning,
                    _ => MessageBoxImage.Information
                };
                MessageBox.Show(n.Message, n.Title, MessageBoxButton.OK, icon);
            };
        }

        public void RefreshFileList()
        {
            _viewModel.Reload();
        }

        private async void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var allSelected = DownloadedFilesGrid.SelectedItems.Cast<InstalledModDisplay>().ToList();
            if (allSelected.Count == 0)
            {
                MessageBox.Show("Select at least one mod to install.");
                return;
            }

            // Filter out rows that can’t be installed (no zip on disk)
            var installable = new List<InstalledModDisplay>();
            var skipped = new List<InstalledModDisplay>();

            foreach (var mod in allSelected)
            {
                string folderName = PathUtils.SanitizeModName(mod.ModName);
                string zipPath = Path.Combine(SettingsService.LoadSettings().OutputDir, folderName, Path.GetFileName(mod.FileName));

                // “IsMissingDownload” is great, but double-check disk too
                if (mod.IsMissingDownload || !File.Exists(zipPath))
                    skipped.Add(mod);
                else
                    installable.Add(mod);
            }

            if (skipped.Count > 0)
            {
                var names = string.Join("\n• ", skipped.Select(m => $"{m.ModName} — {m.FileName}"));
                MessageBox.Show(
                    $"These file(s) can’t be installed because the downloaded .zip is missing:\n\n• {names}",
                    "Some items were skipped",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            if (installable.Count == 0)
            {
                // Nothing left to install
                return;
            }

            var progressWindow = new ProgressWindow(installable.Count);
            progressWindow.Owner = Window.GetWindow(this);
            progressWindow.Show();

            await Task.Run(() =>
            {
                int current = 0;

                foreach (var mod in installable)
                {
                    string folderName = PathUtils.SanitizeModName(mod.ModName);
                    string zipPath = Path.Combine(SettingsService.LoadSettings().OutputDir, folderName, Path.GetFileName(mod.FileName));

                    if (!File.Exists(zipPath))
                    {
                        // Shouldn’t happen now, but be defensive
                        current++;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow.UpdateProgress(current, mod.ModName);
                        });
                        continue;
                    }

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
            _viewModel.RefreshSummary();
        }


        private void UninstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedMods = DownloadedFilesGrid.SelectedItems.Cast<InstalledModDisplay>().ToList();
            if (selectedMods.Count == 0)
            {
                MessageBox.Show("Select at least one mod to uninstall.");
                return;
            }

            var toRemove = new List<InstalledModDisplay>();

            foreach (var mod in selectedMods)
            {
                if (ModInstallerService.UninstallMod(mod.ModName, mod.FileName))
                {
                    mod.Status = "Not Installed";

                    // If the downloaded .zip is gone too, remove the row entirely
                    string folderName = PathUtils.SanitizeModName(mod.ModName);
                    string zipPath = Path.Combine(
                        SettingsService.LoadSettings().OutputDir,
                        folderName,
                        Path.GetFileName(mod.FileName)
                    );

                    if (!File.Exists(zipPath))
                    {
                        toRemove.Add(mod);
                    }
                }
            }

            // Remove after the loop to avoid modifying the collection while iterating
            foreach (var m in toRemove)
            {
                _viewModel.AllDownloadedFiles.Remove(m);
                _viewModel.FilteredDownloadedFiles.Remove(m); // safe even if not present
            }

            DownloadedFilesGrid.Items.Refresh();
            _viewModel.RefreshSummary();

            MessageBox.Show("Selected mods uninstalled.", "Uninstall Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }



        private void ModName_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock tb)
            {
                var modDisplay = tb.DataContext as InstalledModDisplay;
                if (modDisplay == null) return;

                string folderName = modDisplay.ModName;
                string fullPath = Path.Combine(SettingsService.LoadSettings().OutputDir, folderName);

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
