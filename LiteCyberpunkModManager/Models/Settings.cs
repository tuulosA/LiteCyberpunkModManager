using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace LiteCyberpunkModManager.Models
{
    public enum AppTheme
    {
        Dark,
        Light
    }

    public class Settings : INotifyPropertyChanged
    {
        private string _outputDir = DefaultModsDir;
        private string _gameInstallationDir = DefaultGameDir;
        private string _nexusApiKey = "";
        private AppTheme _appTheme = AppTheme.Dark;

        public static string ModCachePath => Path.Combine(DefaultModsDir, "mod_cache.json");

        public string OutputDir
        {
            get => _outputDir;
            set
            {
                if (_outputDir != value)
                {
                    _outputDir = value;
                    OnPropertyChanged();
                }
            }
        }

        public string GameInstallationDir
        {
            get => _gameInstallationDir;
            set
            {
                if (_gameInstallationDir != value)
                {
                    _gameInstallationDir = value;
                    OnPropertyChanged();
                }
            }
        }

        public string NexusApiKey
        {
            get => _nexusApiKey;
            set
            {
                if (_nexusApiKey != value)
                {
                    _nexusApiKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public AppTheme AppTheme
        {
            get => _appTheme;
            set
            {
                if (_appTheme != value)
                {
                    _appTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public static string DefaultGameDir => @"C:\Program Files (x86)\Steam\steamapps\common\Cyberpunk 2077";
        public static string DefaultModsDir => System.IO.Path.Combine(DefaultGameDir, "Mods");
        public static string ArchiveFolder => System.IO.Path.Combine(DefaultGameDir, "archive", "pc", "mod");

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
