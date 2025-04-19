using System;
using System.Windows;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;

namespace CyberpunkModManager.Helpers
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
                    Source = new Uri("/CyberpunkModManager;component/Resources/DarkTheme.xaml", UriKind.Relative)
                };
                window.Resources.MergedDictionaries.Add(darkTheme);

                // Optional: set dark background directly if needed
                if (window.TryFindResource("WindowBackgroundBrush") is System.Windows.Media.Brush bg)
                {
                    window.Background = bg;
                }
            }
            else
            {
                // Light theme = just default system — no resource dictionary
                // Optionally set background to default system
                window.Background = SystemColors.WindowBrush;
            }
        }
    }
}
