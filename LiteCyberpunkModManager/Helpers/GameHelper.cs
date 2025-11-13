using System;
using System.IO;
using LiteCyberpunkModManager.Models;

namespace LiteCyberpunkModManager.Helpers
{
    public static class GameHelper
    {
        public static string GetNexusSlug(GameId game)
        {
            return game switch
            {
                GameId.BaldursGate3 => "baldursgate3",
                _ => "cyberpunk2077"
            };
        }

        public static string GetBg3UserModsDir()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "Larian Studios", "Baldur's Gate 3", "Mods");
        }

        public static string GetBg3ModSettingsPath()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local,
                "Larian Studios",
                "Baldur's Gate 3",
                "PlayerProfiles",
                "Public",
                "modsettings.lsx");
        }

        public static string GetBg3BinDir(Settings s)
        {
            return Path.Combine(s.GameInstallationDir, "bin");
        }

        public static string GetBg3ScriptExtenderDir(Settings s)
        {
            return Path.Combine(GetBg3BinDir(s), "Scripts");
        }
    }
}
