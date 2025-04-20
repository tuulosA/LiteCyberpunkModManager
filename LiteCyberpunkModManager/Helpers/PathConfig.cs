using System.IO;
using LiteCyberpunkModManager.Models;

namespace LiteCyberpunkModManager.Helpers
{
    public static class PathConfig
    {
        public static string ModCache => Path.Combine(Settings.DefaultModsDir, "mod_cache.json");
        public static string DownloadedMods => Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");
        public static string InstalledGameFiles => Path.Combine(Settings.DefaultModsDir, "installed_game_files.json");
        public static string SettingsFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    }
}
