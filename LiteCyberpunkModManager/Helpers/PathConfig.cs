using System;
using System.IO;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Services;

namespace LiteCyberpunkModManager.Helpers
{
    public static class PathConfig
    {
        // Application data root: %LocalAppData%/LiteCPMM
        public static string AppDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiteCPMM");

        // New canonical paths under AppData
        public static string SettingsFile => Path.Combine(AppDataRoot, "settings.json");

        // Per-game cache and metadata files
        private static string CurrentSlug
        {
            get
            {
                try
                {
                    var game = SettingsService.LoadSettings().SelectedGame;
                    return GameHelper.GetNexusSlug(game);
                }
                catch
                {
                    return "cyberpunk2077";
                }
            }
        }

        public static string ModCache => Path.Combine(AppDataRoot, $"mod_cache_{CurrentSlug}.json");
        public static string DownloadedMods => Path.Combine(AppDataRoot, $"downloaded_mods_{CurrentSlug}.json");
        public static string InstalledGameFiles => Path.Combine(AppDataRoot, $"installed_game_files_{CurrentSlug}.json");

        // Legacy file locations (pre-migration) for one-time migration
        public static string LegacySettingsFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        public static string LegacyModCache => Path.Combine(Settings.DefaultModsDir, "mod_cache.json");
        public static string LegacyDownloadedMods => Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");
        public static string LegacyInstalledGameFiles => Path.Combine(Settings.DefaultModsDir, "installed_game_files.json");
    }
}
