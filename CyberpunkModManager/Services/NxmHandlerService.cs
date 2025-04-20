using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using CyberpunkModManager.Models;
using CyberpunkModManager.Views;

namespace CyberpunkModManager.Services
{
    public class NxmHandlerService
    {
        private readonly HttpClient _http = new();
        private const string Game = "cyberpunk2077";
        private const string ApiBase = "https://api.nexusmods.com/v1";

        public async Task HandleAsync(string nxmLink)
        {
            try
            {
                Debug.WriteLine($"[NXM] Received NXM link: {nxmLink}");

                var uri = new Uri(nxmLink);
                var query = HttpUtility.ParseQueryString(uri.Query);

                Debug.WriteLine("==== URI Segments ====");
                foreach (var segment in uri.Segments)
                {
                    Debug.WriteLine($"Segment: {segment}");
                }

                string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathSegments.Length < 4)
                {
                    Debug.WriteLine($"Unexpected path segment count: {pathSegments.Length}. Segments: {string.Join(", ", pathSegments)}");
                    MessageBox.Show("Invalid mod or file ID in .nxm link.", "Invalid Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(pathSegments[1], out int modId) || !int.TryParse(pathSegments[3], out int fileId))
                {
                    Debug.WriteLine($"Failed to parse modId/fileId. Segments: {string.Join(", ", pathSegments)}");
                    MessageBox.Show("Invalid mod or file ID in .nxm link.", "Invalid Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string key = query["key"] ?? "";
                string expires = query["expires"] ?? "";

                Debug.WriteLine($"Parsed modId: {modId}, fileId: {fileId}, key: {key}, expires: {expires}");

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(expires))
                {
                    Debug.WriteLine("Missing key or expires field in the link.");
                    MessageBox.Show("Missing key or expires in the .nxm link. Are you logged in?", "Missing Info", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await DownloadAndTrackAsync(modId, fileId, key, expires);

                if (App.GlobalModListViewModel != null)
                {
                    await App.GlobalModListViewModel.UpdateModStatusAsync(modId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in HandleAsync: {ex}");
                MessageBox.Show($"Failed to handle .nxm link.\n\n{ex.Message}", "NXM Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DownloadAndTrackAsync(int modId, int fileId, string key, string expires)
        {
            Debug.WriteLine("Starting DownloadAndTrackAsync");

            string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

            if (!File.Exists(settingsPath))
            {
                string devPath = @"C:\Users\aleks\source\repos\CyberpunkModManager\CyberpunkModManager\bin\Debug\net8.0-windows\settings.json";
                if (File.Exists(devPath))
                {
                    settingsPath = devPath;
                    Debug.WriteLine($"[Settings] Falling back to dev settings path: {settingsPath}");
                }
                else
                {
                    Debug.WriteLine("settings.json not found at runtime or dev path.");
                    MessageBox.Show("Settings not found. Please launch the app normally first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            var settings = SettingsService.LoadSettings();

            if (string.IsNullOrWhiteSpace(settings.NexusApiKey))
            {
                Debug.WriteLine("API key is missing or empty.");
                MessageBox.Show("Please enter your Nexus Mods API key in the Settings tab.", "API Key Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string metadataPath = Path.Combine(Settings.DefaultModsDir, "downloaded_mods.json");

            string linkUrl = $"{ApiBase}/games/{Game}/mods/{modId}/files/{fileId}/download_link.json?key={key}&expires={expires}";
            Debug.WriteLine($"Fetching download link: {linkUrl}");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", settings.NexusApiKey);

            var response = await _http.GetAsync(linkUrl);
            Debug.WriteLine($"Download link response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show($"Failed to get download link: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(content).RootElement;

            string downloadUrl = root[0].GetProperty("URI").GetString() ?? "";
            Debug.WriteLine($"Download URL: {downloadUrl}");

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Debug.WriteLine("Download URL is empty.");
                MessageBox.Show("Failed to extract download URL.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            string modName = await GetModNameAsync(modId);
            Debug.WriteLine($"Resolved mod name: {modName}");

            string modFolder = Path.Combine(Settings.DefaultModsDir, PathUtils.SanitizeModName(modName));
            Directory.CreateDirectory(modFolder);
            string fullPath = Path.Combine(modFolder, fileName);

            Debug.WriteLine($"Downloading file to: {fullPath}");

            using var download = await _http.GetAsync(downloadUrl);
            await using var stream = await download.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(fullPath, FileMode.Create);
            await stream.CopyToAsync(fs);

            Debug.WriteLine($"Successfully downloaded file: {fileName}");

            var entry = new InstalledModInfo
            {
                ModId = modId,
                ModName = modName,
                FileId = fileId,
                FileName = fileName,
                UploadedTimestamp = DateTime.UtcNow
            };

            var list = new System.Collections.Generic.List<InstalledModInfo>();
            if (File.Exists(metadataPath))
            {
                try
                {
                    string json = File.ReadAllText(metadataPath);
                    list = JsonSerializer.Deserialize<System.Collections.Generic.List<InstalledModInfo>>(json) ?? new();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to deserialize existing metadata: {ex.Message}");
                }
            }

            list.RemoveAll(x => x.ModId == modId && x.FileId == fileId);
            list.Add(entry);

            File.WriteAllText(metadataPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));

            Debug.WriteLine($"Metadata updated for modId: {modId}, fileId: {fileId}");

            // refresh FilesView
            App.Current.Dispatcher.Invoke(() =>
            {
                App.GlobalFilesView?.RefreshFileList();
            });

            MessageBox.Show($"Successfully downloaded: {fileName}", "NXM Download", MessageBoxButton.OK, MessageBoxImage.Information);

        }

        private async Task<string> GetModNameAsync(int modId)
        {
            var url = $"{ApiBase}/games/{Game}/mods/{modId}.json";
            Debug.WriteLine($"Fetching mod name from: {url}");

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Failed to fetch mod name. Status: {response.StatusCode}");
                return $"Mod_{modId}";
            }

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(json).RootElement;

            if (root.TryGetProperty("name", out var nameProp))
            {
                string name = nameProp.GetString() ?? $"Mod_{modId}";
                Debug.WriteLine($"Fetched mod name: {name}");
                return name;
            }

            Debug.WriteLine("Mod name property not found, falling back.");
            return $"Mod_{modId}";
        }
    }
}
