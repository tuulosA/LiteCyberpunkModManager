using System.Windows;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;

namespace LiteCyberpunkModManager.Helpers
{
    public static class ThemeHelper
    {
        public static void ApplyThemeTo(Window window)
        {
            var settings = SettingsService.LoadSettings();

            window.Resources.MergedDictionaries.Clear();

            if (settings.AppTheme == AppTheme.Dark)
            {
                var darkTheme = new ResourceDictionary
                {
                    Source = new Uri("/HMM;component/Resources/DarkTheme.xaml", UriKind.Relative)
                };
                window.Resources.MergedDictionaries.Add(darkTheme);

                if (window.TryFindResource("WindowBackgroundBrush") is System.Windows.Media.Brush bg)
                {
                    window.Background = bg;
                }
            }
            else
            {
                window.Background = SystemColors.WindowBrush;
            }
        }
    }
}
