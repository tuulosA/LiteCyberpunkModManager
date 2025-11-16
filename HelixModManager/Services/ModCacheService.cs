using System.Diagnostics;
using System.IO;
using System.Text.Json;
using HelixModManager.Helpers;
using HelixModManager.Models;

namespace HelixModManager.Services
{
    public static class ModCacheService
    {
        public static List<Mod>? LoadCachedMods()
        {
            var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
            var path = Path.Combine(PathConfig.AppDataRoot, $"mod_cache_{slug}.json");
            Debug.WriteLine($"[CACHE] Attempting to load mod cache from: {path}");

            // Ensure directory exists and migrate legacy cache if present
            try
            {
                Directory.CreateDirectory(PathConfig.AppDataRoot);
                // Only migrate the legacy cache into the per-game file for Cyberpunk.
                // Avoid copying Cyberpunk cache into BG3 cache.
                if (slug == "cyberpunk2077" && !File.Exists(path) && File.Exists(PathConfig.LegacyModCache))
                {
                    File.Copy(PathConfig.LegacyModCache, path, overwrite: false);
                    Debug.WriteLine($"[CACHE] Migrated legacy mod cache from {PathConfig.LegacyModCache} -> {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CACHE ERROR] Migration/ensure directory failed: {ex.Message}");
            }

            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                var result = JsonSerializer.Deserialize<List<Mod>>(json);
                Debug.WriteLine($"[CACHE] Loaded {result?.Count ?? 0} mods from cache.");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CACHE ERROR] Failed to load cache: {ex.Message}");
                return null;
            }
        }

        public static void SaveCachedMods(List<Mod> mods)
        {
            var slug = GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
            var path = Path.Combine(PathConfig.AppDataRoot, $"mod_cache_{slug}.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(mods, options));
                Debug.WriteLine($"[CACHE] Saved {mods.Count} mods to: {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CACHE ERROR] Failed to save cache: {ex.Message}");
            }
        }
    }
}

