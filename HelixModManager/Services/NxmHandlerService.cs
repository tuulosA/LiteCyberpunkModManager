using System.IO;
using System.Net.Http;
using System.Text.Json;
using System;
using System.Web;
using System.Windows;
using System.Diagnostics;
using HelixModManager.Models;
using HelixModManager.Views;
using HelixModManager.Helpers;

namespace HelixModManager.Services
{
    public class NxmHandlerService
    {
        private readonly HttpClient _http = new();
        private string CurrentGameSlug => GameHelper.GetNexusSlug(SettingsService.LoadSettings().SelectedGame);
        private const string ApiBase = "https://api.nexusmods.com/v1";
        public event Action<Notification>? NotificationRaised;

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
                    NotificationRaised?.Invoke(new Notification("Invalid Link", "Invalid mod or file ID in .nxm link.", NotificationType.Warning));
                    return;
                }

                if (!int.TryParse(pathSegments[1], out int modId) || !int.TryParse(pathSegments[3], out int fileId))
                {
                    Debug.WriteLine($"Failed to parse modId/fileId. Segments: {string.Join(", ", pathSegments)}");
                    NotificationRaised?.Invoke(new Notification("Invalid Link", "Invalid mod or file ID in .nxm link.", NotificationType.Warning));
                    return;
                }

                string key = query["key"] ?? "";
                string expires = query["expires"] ?? "";

                Debug.WriteLine($"Parsed modId: {modId}, fileId: {fileId}, key: {key}, expires: {expires}");

                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(expires))
                {
                    Debug.WriteLine("Missing key or expires field in the link.");
                    NotificationRaised?.Invoke(new Notification("Missing Info", "Missing key or expires in the .nxm link. Are you logged in?", NotificationType.Warning));
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
                NotificationRaised?.Invoke(new Notification("NXM Error", $"Failed to handle .nxm link.\n\n{ex.Message}", NotificationType.Error));
            }
        }


        private async Task<DateTime> GetFileUploadTimeAsync(int modId, int fileId)
        {
            string url = $"{ApiBase}/games/{CurrentGameSlug}/mods/{modId}/files/{fileId}.json";
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", SettingsService.LoadSettings().NexusApiKey);

            try
            {
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[WARN] Could not fetch file upload time: {response.StatusCode}");
                    return DateTime.UtcNow;
                }

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                if (root.TryGetProperty("uploaded_timestamp", out var timestampProp))
                {
                    long unixTimestamp = timestampProp.GetInt64();
                    return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Exception fetching file upload time: {ex}");
            }

            return DateTime.UtcNow;
        }


        private async Task<string> GetFileDisplayNameAsync(int modId, int fileId)
        {
            string url = $"{ApiBase}/games/{CurrentGameSlug}/mods/{modId}/files/{fileId}.json";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new Exception("Failed to fetch file metadata");

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(json).RootElement;

            return root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? $"file_{fileId}" : $"file_{fileId}";
        }


        private async Task DownloadAndTrackAsync(int modId, int fileId, string key, string expires)
        {
            Debug.WriteLine("Starting DownloadAndTrackAsync");

            var settings = SettingsService.LoadSettings();

            if (string.IsNullOrWhiteSpace(settings.NexusApiKey))
            {
                Debug.WriteLine("API key is missing or empty.");
                NotificationRaised?.Invoke(new Notification("API Key Missing", "Please enter your Nexus Mods API key in the Settings tab.", NotificationType.Warning));
                return;
            }

            string metadataPath = PathConfig.DownloadedMods;
            Directory.CreateDirectory(PathConfig.AppDataRoot);

            string linkUrl = $"{ApiBase}/games/{CurrentGameSlug}/mods/{modId}/files/{fileId}/download_link.json?key={key}&expires={expires}";
            Debug.WriteLine($"Fetching download link: {linkUrl}");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("apikey", settings.NexusApiKey);

            var response = await _http.GetAsync(linkUrl);
            Debug.WriteLine($"Download link response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                NotificationRaised?.Invoke(new Notification("Error", $"Failed to get download link: {response.StatusCode}", NotificationType.Error));
                return;
            }

            var content = await response.Content.ReadAsStringAsync();
            var root = JsonDocument.Parse(content).RootElement;

            string downloadUrl = root[0].GetProperty("URI").GetString() ?? "";
            Debug.WriteLine($"Download URL: {downloadUrl}");

            if (string.IsNullOrEmpty(downloadUrl))
            {
                Debug.WriteLine("Download URL is empty.");
                NotificationRaised?.Invoke(new Notification("Error", "Failed to extract download URL.", NotificationType.Error));
                return;
            }

            // Get clean file name from Nexus API (use the 'name' field, not download URL parsing)
            string fileDisplayName = await GetFileDisplayNameAsync(modId, fileId);
            string fileName = FileNameHelper.NormalizeDisplayFileName(fileDisplayName);

            string modName = await GetModNameAsync(modId);
            Debug.WriteLine($"Resolved mod name: {modName}");

            // reuse settings from above
            string modFolder = Path.Combine(settings.OutputDir, PathUtils.SanitizeModName(modName));
            Directory.CreateDirectory(modFolder);
            string fullPath = Path.Combine(modFolder, fileName);

            Debug.WriteLine($"Downloading file to: {fullPath}");

            var progressWindow = new ProgressBarWindow();
            progressWindow.Owner = Application.Current.MainWindow;
            progressWindow.Show();

            try
            {
                using var download = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                await using var stream = await download.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(fullPath, FileMode.Create);

                var buffer = new byte[81920];
                long totalBytes = download.Content.Headers.ContentLength ?? -1;
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fs.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes > 0)
                    {
                        double progress = totalRead / (double)totalBytes * 100;
                        Application.Current.Dispatcher.Invoke(() => progressWindow.SetProgress(progress));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Download] Error during file stream: {ex}");
                NotificationRaised?.Invoke(new Notification("Error", $"Download failed:\n\n{ex.Message}", NotificationType.Error));
                return;
            }
            finally
            {
                progressWindow.Close();
            }

            Debug.WriteLine($"Successfully downloaded file: {fileName}");

            var entry = new InstalledModInfo
            {
                ModId = modId,
                ModName = modName,
                FileId = fileId,
                FileName = fileName,
                UploadedTimestamp = await GetFileUploadTimeAsync(modId, fileId),
                Game = SettingsService.LoadSettings().SelectedGame
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

            list.RemoveAll(x =>
                x.ModId == modId &&
                x.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            list.Add(entry);

            File.WriteAllText(metadataPath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));

            Debug.WriteLine($"Metadata updated for modId: {modId}, fileId: {fileId}");

            App.Current.Dispatcher.Invoke(() =>
            {
                App.GlobalFilesView?.RefreshFileList();
            });

            NotificationRaised?.Invoke(new Notification("NXM Download", $"Successfully downloaded: {fileName}", NotificationType.Info));
        }


        private async Task<string> GetModNameAsync(int modId)
        {
            var url = $"{ApiBase}/games/{CurrentGameSlug}/mods/{modId}.json";
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

