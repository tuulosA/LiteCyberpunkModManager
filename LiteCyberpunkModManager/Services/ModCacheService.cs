using System.IO;
using System.Text.Json;
using LiteCyberpunkModManager.Helpers;
using LiteCyberpunkModManager.Models;

namespace LiteCyberpunkModManager.Services
{
    public static class ModCacheService
    {
        public static List<Mod>? LoadCachedMods()
        {
            var path = PathConfig.ModCache;
            Console.WriteLine($"[CACHE] Attempting to load mod cache from: {path}");

            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                var result = JsonSerializer.Deserialize<List<Mod>>(json);
                Console.WriteLine($"[CACHE] Loaded {result?.Count ?? 0} mods from cache.");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CACHE ERROR] Failed to load cache: {ex.Message}");
                return null;
            }
        }

        public static void SaveCachedMods(List<Mod> mods)
        {
            var path = PathConfig.ModCache;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(mods, options));
                Console.WriteLine($"[CACHE] Saved {mods.Count} mods to: {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CACHE ERROR] Failed to save cache: {ex.Message}");
            }
        }
    }
}
