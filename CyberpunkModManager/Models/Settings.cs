using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CyberpunkModManager.Models
{
    public class Settings : INotifyPropertyChanged
    {
        private string _outputDir = DefaultModsDir;
        private string _gameInstallationDir = DefaultGameDir;
        private string _nexusApiKey = "";

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

        // Default paths
        public static string DefaultGameDir => @"C:\Program Files (x86)\Steam\steamapps\common\Cyberpunk 2077";
        public static string DefaultModsDir => System.IO.Path.Combine(DefaultGameDir, "Mods");
        public static string ArchiveFolder => System.IO.Path.Combine(DefaultGameDir, "archive", "pc", "mod");

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
