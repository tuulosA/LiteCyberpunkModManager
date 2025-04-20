using System;
using System.Windows;
using CyberpunkModManager.Models;
using CyberpunkModManager.Services;

namespace CyberpunkModManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var settings = SettingsService.LoadSettings();

            var themeDict = new ResourceDictionary();

            if (settings.AppTheme == AppTheme.Dark)
            {
                themeDict.Source = new Uri("/CyberpunkModManager;component/Resources/DarkTheme.xaml", UriKind.Relative);
            }
            else
            {
                themeDict.Source = new Uri("/CyberpunkModManager;component/Resources/LightTheme.xaml", UriKind.Relative);
            }

            Resources.MergedDictionaries.Add(themeDict);
        }


    }
}
