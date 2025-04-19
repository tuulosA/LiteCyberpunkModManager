using System.Windows;
using System.Windows.Controls;
using CyberpunkModManager.ViewModels;
using CyberpunkModManager.Services;
using System.IO;
using System.Linq;
using CyberpunkModManager.Models;

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

        private void Install_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is InstalledModDisplay mod)
            {
                string folderName = PathUtils.SanitizeModName(mod.ModName);
                string zipPath = Path.Combine(Settings.DefaultModsDir, folderName, Path.GetFileNameWithoutExtension(mod.FileName) + ".zip");

                if (!File.Exists(zipPath))
                {
                    MessageBox.Show("Mod zip file not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                bool success = ModInstallerService.InstallModFile(zipPath, mod.ModName, mod.FileName, out var installedPaths);
                if (success)
                {
                    MessageBox.Show("Mod installed successfully!", "Installed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Installation failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                RefreshFileList();
            }
        }

        private void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is InstalledModDisplay mod)
            {
                bool success = ModInstallerService.UninstallMod(mod.ModName);
                if (success)
                {
                    MessageBox.Show("Mod uninstalled from game files.", "Uninstalled", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Uninstallation failed or mod wasn't installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                RefreshFileList();
            }
        }
    }
}
