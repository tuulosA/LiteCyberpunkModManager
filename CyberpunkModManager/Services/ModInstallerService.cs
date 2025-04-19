using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using CyberpunkModManager.Models;

namespace CyberpunkModManager.Services
{
    public static class ModInstallerService
    {
        private static Settings Settings => SettingsService.LoadSettings();
        private static string GameDir => Settings.GameInstallationDir;
        private static string ModsDir => Settings.OutputDir;
        private static string InstalledJsonPath => Path.Combine(ModsDir, "installed_game_files.json");


        public static bool InstallModFile(string zipPath, string modName, string zipFileName, out List<string> installedPaths)
        {
            installedPaths = new();
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"ModInstall_{Guid.NewGuid()}");

            try
            {
                ZipFile.ExtractToDirectory(zipPath, tempExtractDir);

                var archiveFiles = Directory.GetFiles(tempExtractDir, "*.archive", SearchOption.AllDirectories);
                bool onlyArchives = archiveFiles.Length > 0 && Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories).All(f => f.EndsWith(".archive"));

                if (onlyArchives)
                {
                    string targetDir = Path.Combine(GameDir, "archive", "pc", "mod");
                    Directory.CreateDirectory(targetDir);

                    foreach (var file in archiveFiles)
                    {
                        string targetPath = Path.Combine(targetDir, Path.GetFileName(file));
                        File.Copy(file, targetPath, overwrite: true);
                        installedPaths.Add(targetPath);
                    }
                }
                else
                {
                    foreach (var file in Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(tempExtractDir, file);
                        string targetPath = Path.Combine(GameDir, relative);

                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.Copy(file, targetPath, overwrite: true);
                        installedPaths.Add(targetPath);
                    }
                }

                SaveInstallRecord(modName, zipFileName, installedPaths);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Install failed: {ex.Message}");
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true); } catch { }
            }
        }

        public static bool UninstallMod(string modName)
        {
            if (!File.Exists(InstalledJsonPath)) return false;

            try
            {
                var list = JsonSerializer.Deserialize<List<InstalledGameFile>>(File.ReadAllText(InstalledJsonPath)) ?? new();
                var modEntry = list.FirstOrDefault(m => m.ModName.Equals(modName, StringComparison.OrdinalIgnoreCase));

                if (modEntry == null) return false;

                foreach (var path in modEntry.InstalledPaths)
                {
                    if (File.Exists(path))
                    {
                        try { File.Delete(path); }
                        catch (Exception ex) { Console.WriteLine($"[WARN] Could not delete: {path} - {ex.Message}"); }
                    }
                }

                list.Remove(modEntry);
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(InstalledJsonPath, JsonSerializer.Serialize(list, options));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Uninstall failed: {ex.Message}");
                return false;
            }
        }

        private static void SaveInstallRecord(string modName, string fileName, List<string> paths)
        {
            List<InstalledGameFile> list = new();
            if (File.Exists(InstalledJsonPath))
            {
                try
                {
                    string json = File.ReadAllText(InstalledJsonPath);
                    list = JsonSerializer.Deserialize<List<InstalledGameFile>>(json) ?? new();
                }
                catch { }
            }

            list.RemoveAll(m => m.ModName == modName);
            list.Add(new InstalledGameFile
            {
                ModName = modName,
                FileName = fileName,
                InstalledPaths = paths
            });

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(InstalledJsonPath, JsonSerializer.Serialize(list, options));
        }
    }

    public class InstalledGameFile
    {
        public string ModName { get; set; } = "";
        public string FileName { get; set; } = "";
        public List<string> InstalledPaths { get; set; } = new();
    }
}
