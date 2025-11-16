using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HelixModManager.Helpers;

namespace HelixModManager.Models
{
    public enum AppTheme
    {
        Dark,
        Light
    }

    public class Settings : INotifyPropertyChanged
    {
        private string _outputDir;
        private string _gameInstallationDir;
        private string _nexusApiKey = "";
        private AppTheme _appTheme = AppTheme.Dark;
        private GameId _selectedGame = GameId.Cyberpunk2077;

        public Settings()
        {
            _outputDir = GetDefaultOutputDir(_selectedGame);
            _gameInstallationDir = GetDefaultGameInstallationDir(_selectedGame);
        }

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

        public GameId SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != value)
                {
                    var previousGame = _selectedGame;
                    _selectedGame = value;
                    OnPropertyChanged();
                    UpdateOutputDirIfDefault(previousGame);
                    UpdateGameInstallationDirIfDefault(previousGame);
                }
            }
        }

        public static string DefaultGameDir => @"C:\Program Files (x86)\Steam\steamapps\common\Cyberpunk 2077";
        public static string DefaultModsDir => System.IO.Path.Combine(DefaultGameDir, "Mods");
        public static string DefaultBg3GameDir => @"C:\Program Files (x86)\Steam\steamapps\common\Baldurs Gate 3";
        public static string GetDefaultOutputDir(GameId game) =>
            game switch
            {
                GameId.BaldursGate3 => GameHelper.GetBg3UserModsDir(),
                _ => DefaultModsDir
            };
        public static string GetDefaultGameInstallationDir(GameId game) =>
            game switch
            {
                GameId.BaldursGate3 => DefaultBg3GameDir,
                _ => DefaultGameDir
            };
        public static string ArchiveFolder => System.IO.Path.Combine(DefaultGameDir, "archive", "pc", "mod");


        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateOutputDirIfDefault(GameId previousGame)
        {
            var previousDefault = GetDefaultOutputDir(previousGame);
            if (string.Equals(_outputDir, previousDefault, StringComparison.OrdinalIgnoreCase))
            {
                OutputDir = GetDefaultOutputDir(_selectedGame);
            }
        }

        private void UpdateGameInstallationDirIfDefault(GameId previousGame)
        {
            var previousDefault = GetDefaultGameInstallationDir(previousGame);
            if (string.Equals(_gameInstallationDir, previousDefault, StringComparison.OrdinalIgnoreCase))
            {
                GameInstallationDir = GetDefaultGameInstallationDir(_selectedGame);
            }
        }
    }

}

