using System.IO;
using System.Text.Json;
using System;
using LiteCyberpunkModManager.Models;
using SharpCompress.Archives;
using SharpCompress.Common;
using LiteCyberpunkModManager.Helpers;
using System.Diagnostics;


namespace LiteCyberpunkModManager.Services
{
    public static class ModInstallerService
    {
        public static event Action<Notification>? NotificationRaised;
        private static Settings Settings => SettingsService.LoadSettings();
        private static string GameDir => Settings.GameInstallationDir;
        private static string ModsDir => Settings.OutputDir;


        public static bool InstallModFile(
            string zipPath,
            string modName,
            string zipFileName,
            out List<string> installedPaths,
            Action<int, string>? onExtractProgress = null)
        {
            installedPaths = new();
            string tempExtractDir = Path.Combine(Path.GetTempPath(), $"ModInstall_{Guid.NewGuid()}");

            string ext = Path.GetExtension(zipPath).ToLowerInvariant();
            if (ext != ".zip" && ext != ".rar" && ext != ".7z")
            {
                NotificationRaised?.Invoke(new Notification(
                    "Unsupported Format",
                    "Only .zip, .rar or .7z files are supported.",
                    NotificationType.Warning));
                return false;
            }

            try
            {
                Directory.CreateDirectory(tempExtractDir);

                using var archive = ArchiveFactory.Open(zipPath);
                var fileEntries = archive.Entries.Where(entry => !entry.IsDirectory).ToList();
                int totalFiles = fileEntries.Count;
                int currentFile = 0;

                foreach (var entry in fileEntries)
                {
                    entry.WriteToDirectory(tempExtractDir, new ExtractionOptions
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });

                    currentFile++;
                    int progress = (int)((currentFile / (double)totalFiles) * 100);

                    onExtractProgress?.Invoke(progress, entry.Key!);
                }
            }
            catch (Exception ex)
            {
                NotificationRaised?.Invoke(new Notification(
                    "Extraction Error",
                    "Failed to extract the archive. It may be corrupted or unsupported.\n\n" + ex.Message,
                    NotificationType.Error));
                return false;
            }


            try
            {
                var allFiles = Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories);
                var archiveFiles = allFiles.Where(f => f.EndsWith(".archive", StringComparison.OrdinalIgnoreCase)).ToList();
                bool onlyArchives = archiveFiles.Count > 0 && allFiles.All(f => f.EndsWith(".archive", StringComparison.OrdinalIgnoreCase));

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
                    string commonPrefix = GetCommonTopLevelFolder(allFiles, tempExtractDir);

                    foreach (var file in allFiles)
                    {

                        string relativePath;

                        // normalize prefix
                        string normalizedPrefix = commonPrefix.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        // check if a single root folder
                        bool allUnderOneTopLevel = allFiles
                            .Select(f => Path.GetRelativePath(tempExtractDir, f))
                            .All(rel => rel.StartsWith(normalizedPrefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

                        // protected folders we never want to strip
                        string[] protectedRoots = { "r6", "archive", "red4ext", "bin", "engine" };

                        // check if first folder is a protected root
                        bool containsProtectedRoot = allFiles
                            .Select(f => Path.GetRelativePath(tempExtractDir, f).Split(Path.DirectorySeparatorChar)[0].ToLowerInvariant())
                            .Any(folder => protectedRoots.Contains(folder));

                        // strip only if all files are under a single non-protected top folder
                        if (!string.IsNullOrEmpty(commonPrefix) && allUnderOneTopLevel && !containsProtectedRoot)
                        {
                            relativePath = Path.GetRelativePath(Path.Combine(tempExtractDir, commonPrefix), file);
                        }
                        else
                        {
                            relativePath = Path.GetRelativePath(tempExtractDir, file);
                        }

                        string targetPath = Path.Combine(GameDir, relativePath);
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
                Debug.WriteLine($"[ERROR] Install failed: {ex.Message}");
                NotificationRaised?.Invoke(new Notification(
                    "Install Error",
                    "Mod installation failed after extraction. Files might be invalid.",
                    NotificationType.Error));
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempExtractDir)) Directory.Delete(tempExtractDir, true); } catch { }
            }
        }

        private static string GetCommonTopLevelFolder(IEnumerable<string> filePaths, string root)
        {
            var relativePaths = filePaths
                .Select(f => Path.GetRelativePath(root, f))
                .Select(f => f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Where(parts => parts.Length > 1)
                .ToList();

            if (!relativePaths.Any()) return "";

            string prefix = relativePaths[0][0];
            bool allMatch = relativePaths.All(parts => parts[0] == prefix);

            return allMatch ? prefix + Path.DirectorySeparatorChar : "";
        }

        public static bool UninstallMod(string modName, string fileName)
        {
            // Ensure app data dir and migrate legacy install tracking if needed
            try
            {
                Directory.CreateDirectory(PathConfig.AppDataRoot);
                if (!File.Exists(PathConfig.InstalledGameFiles) && File.Exists(PathConfig.LegacyInstalledGameFiles))
                {
                    File.Copy(PathConfig.LegacyInstalledGameFiles, PathConfig.InstalledGameFiles, overwrite: false);
                }
            }
            catch { }

            if (!File.Exists(PathConfig.InstalledGameFiles)) return false;

            try
            {
                var list = JsonSerializer.Deserialize<List<InstalledGameFile>>(File.ReadAllText(PathConfig.InstalledGameFiles)) ?? new();
                var matchingEntries = list.Where(m =>
                    m.ModName.Equals(modName, StringComparison.OrdinalIgnoreCase) &&
                    m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)).ToList();

                if (matchingEntries.Count == 0) return false;

                foreach (var modEntry in matchingEntries)
                {
                    foreach (var path in modEntry.InstalledPaths)
                    {
                        if (File.Exists(path))
                        {
                            try { File.Delete(path); }
                            catch (Exception ex) { Debug.WriteLine($"[WARN] Could not delete: {path} - {ex.Message}"); }
                        }
                    }

                    list.Remove(modEntry);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                Directory.CreateDirectory(PathConfig.AppDataRoot);
                File.WriteAllText(PathConfig.InstalledGameFiles, JsonSerializer.Serialize(list, options));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Uninstall failed: {ex.Message}");
                return false;
            }
        }


        private static void SaveInstallRecord(string modName, string fileName, List<string> paths)
        {
            List<InstalledGameFile> list = new();
            if (File.Exists(PathConfig.InstalledGameFiles))
            {
                try
                {
                    string json = File.ReadAllText(PathConfig.InstalledGameFiles);
                    list = JsonSerializer.Deserialize<List<InstalledGameFile>>(json) ?? new();
                }
                catch { }
            }

            // only remove the entry for specific file, not the whole mod
            list.RemoveAll(m =>
                m.ModName.Equals(modName, StringComparison.OrdinalIgnoreCase) &&
                m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            list.Add(new InstalledGameFile
            {
                ModName = modName,
                FileName = fileName,
                InstalledPaths = paths
            });

            var options = new JsonSerializerOptions { WriteIndented = true };
            Directory.CreateDirectory(PathConfig.AppDataRoot);
            File.WriteAllText(PathConfig.InstalledGameFiles, JsonSerializer.Serialize(list, options));
        }


    }

    public class InstalledGameFile
    {
        public string ModName { get; set; } = "";
        public string FileName { get; set; } = "";
        public List<string> InstalledPaths { get; set; } = new();
    }
}
