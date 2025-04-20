using System.Windows;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;
using LiteCyberpunkModManager.ViewModels;
using Microsoft.Win32;
using System.Diagnostics;
using LiteCyberpunkModManager.Views;

namespace LiteCyberpunkModManager
{
    public partial class App : Application
    {
        public static ModListViewModel? GlobalModListViewModel { get; set; }
        public static FilesView? GlobalFilesView { get; set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Debug.WriteLine("==== Application Startup ====");

            RegisterNxmProtocol();

            bool isPrimary = SingleInstanceManager.InitializeAsPrimary();

            if (!isPrimary)
            {
                if (e.Args.Length > 0 && e.Args[0].StartsWith("nxm://"))
                {
                    await SingleInstanceManager.SendNxmLinkToPrimaryAsync(e.Args[0]);
                }
                Shutdown();
                return;
            }

            // start the pipe server to listen for forwarded links
            SingleInstanceManager.StartPipeServer(async link =>
            {
                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    var handler = new NxmHandlerService();
                    await handler.HandleAsync(link);
                });
            });

            var settings = SettingsService.LoadSettings();
            var themeUri = settings.AppTheme == AppTheme.Dark
                ? "/LiteCPMM;component/Resources/DarkTheme.xaml"
                : "/LiteCPMM;component/Resources/LightTheme.xaml";

            Debug.WriteLine($"Loading theme: {themeUri}");

            var themeDict = new ResourceDictionary
            {
                Source = new Uri(themeUri, UriKind.Relative)
            };
            Resources.MergedDictionaries.Add(themeDict);

            // handle the link on initial launch
            if (e.Args.Length > 0 && e.Args[0].StartsWith("nxm://"))
            {
                await HandleNxmLinkAsync(e.Args[0]);
            }
        }

        private void RegisterNxmProtocol()
        {
            try
            {
                var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\nxm");
                key?.SetValue("", "URL:Nexus Mod Protocol");
                key?.SetValue("URL Protocol", "");

                var commandKey = key?.CreateSubKey(@"shell\open\command");

                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "LiteCPMM.exe";
                commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");

                Debug.WriteLine($"NXM protocol registered. exePath = {exePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register NXM protocol: {ex}");
            }
        }

        private async Task HandleNxmLinkAsync(string link)
        {
            try
            {
                Debug.WriteLine($"Handling .nxm link: {link}");
                var nxmHandler = new NxmHandlerService();
                await nxmHandler.HandleAsync(link);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception during .nxm link handling: {ex}");
                MessageBox.Show($"Failed to handle .nxm link:\n\n{ex.Message}", "NXM Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
