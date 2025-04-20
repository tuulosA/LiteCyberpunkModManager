using System.Diagnostics;
using System.IO;
using System.Text.Json;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Helpers;

namespace LiteCyberpunkModManager.Services
{
    public class SettingsService
    {

        public static Settings LoadSettings()
        {
            try
            {
                if (File.Exists(PathConfig.SettingsFile))
                {
                    Debug.WriteLine($"[SettingsService] Found settings.json at: {Path.GetFullPath(PathConfig.SettingsFile)}");

                    var json = File.ReadAllText(PathConfig.SettingsFile);
                    Debug.WriteLine($"[SettingsService] JSON content: {json}");

                    var settings = JsonSerializer.Deserialize<Settings>(json);

                    if (settings != null)
                    {
                        Debug.WriteLine($"[SettingsService] Loaded OutputDir: {settings.OutputDir}");
                        Debug.WriteLine($"[SettingsService] Loaded GameInstallationDir: {settings.GameInstallationDir}");
                        return settings;
                    }

                    Debug.WriteLine("[SettingsService] Deserialized settings were null, using defaults.");
                }
                else
                {
                    Debug.WriteLine("[SettingsService] settings.json not found. Using default settings.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Exception while loading: {ex.Message}");
            }

            var defaultSettings = new Settings();
            SaveSettings(defaultSettings);

            Debug.WriteLine($"[SettingsService] Default settings created:");
            Debug.WriteLine($"    OutputDir: {defaultSettings.OutputDir}");
            Debug.WriteLine($"    GameInstallationDir: {defaultSettings.GameInstallationDir}");

            return defaultSettings;
        }

        public static void SaveSettings(Settings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PathConfig.SettingsFile, json);
                Debug.WriteLine("[SettingsService] Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SettingsService] Failed to save settings: {ex.Message}");
            }
        }
    }
}
