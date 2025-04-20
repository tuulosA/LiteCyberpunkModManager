using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Web;
using System.Windows;
using System.Diagnostics;
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Views;

namespace LiteCyberpunkModManager.Services
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


        private async Task<DateTime> GetFileUploadTimeAsync(int modId, int fileId)
        {
            string url = $"{ApiBase}/games/{Game}/mods/{modId}/files/{fileId}.json";
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


        private async Task DownloadAndTrackAsync(int modId, int fileId, string key, string expires)
        {
            Debug.WriteLine("Starting DownloadAndTrackAsync");

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

            string rawFileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);

            // strip versioning/timestamp
            string nameWithoutExt = Path.GetFileNameWithoutExtension(rawFileName);
            string[] parts = nameWithoutExt.Split('-');
            string strippedBase = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out _))
                    strippedBase += "-" + parts[i];
                else
                    break;
            }
            string fileName = $"{strippedBase}.zip";

            string modName = await GetModNameAsync(modId);
            Debug.WriteLine($"Resolved mod name: {modName}");

            string modFolder = Path.Combine(Settings.DefaultModsDir, PathUtils.SanitizeModName(modName));
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
                MessageBox.Show($"Download failed:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                FileName = Path.GetFileNameWithoutExtension(fileName),
                UploadedTimestamp = await GetFileUploadTimeAsync(modId, fileId)
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
