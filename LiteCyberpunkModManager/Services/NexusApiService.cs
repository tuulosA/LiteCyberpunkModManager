using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
// Removed direct UI dependency; UI should subscribe to notifications
using LiteCyberpunkModManager.Models;
using LiteCyberpunkModManager.Helpers;

namespace LiteCyberpunkModManager.Services
{
    public class NexusApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.nexusmods.com/v1";
        private string _apiKey;
        [ThreadStatic] private static bool _localRateLimitShown;

        public event Action<Notification>? NotificationRaised;

        public void SetApiKey(string newApiKey)
        {
            _apiKey = newApiKey;

            if (_httpClient.DefaultRequestHeaders.Contains("apikey"))
                _httpClient.DefaultRequestHeaders.Remove("apikey");

            _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);
        }

        public NexusApiService(string apiKey)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);
        }

        public async Task<bool> IsPremiumUserAsync()
        {
            var url = $"{BaseUrl}/users/validate.json";
            try
            {
                var response = await GetAsync(url);
                if (response == null) return false;

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                return root.TryGetProperty("is_premium", out var premiumProp) && premiumProp.GetBoolean();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to check premium status: {ex.Message}");
                return false;
            }
        }


        private async Task<HttpResponseMessage?> GetAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Debug.WriteLine("[ERROR] Rate limit reached for URL: " + url);

                if (!_localRateLimitShown)
                {
                    _localRateLimitShown = true;
                    NotificationRaised?.Invoke(new Notification(
                        "Rate Limit Triggered",
                        "Nexus Mods API rate limit reached. Try again later.",
                        NotificationType.Warning));
                }

                return null;
            }

            return response;
        }


        public async Task<string?> GetDownloadLinkAsync(string game, int modId, int fileId)
        {
            var url = $"{BaseUrl}/games/{game}/mods/{modId}/files/{fileId}/download_link.json";

            try
            {
                var response = await GetAsync(url);
                if (response == null) return null;

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    NotificationRaised?.Invoke(new Notification(
                        "Premium Required",
                        "This feature requires a Nexus Mods Premium account.\nPlease log in to Nexus with a Premium account to use API-based downloads,\nor use Mod Manager Download links on Nexus Mods.",
                        NotificationType.Info));
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var links = JsonDocument.Parse(json).RootElement;

                if (links.ValueKind == JsonValueKind.Array && links.GetArrayLength() > 0)
                {
                    return links[0].GetProperty("URI").GetString();
                }

                Debug.WriteLine($"No download links returned for file {fileId} of mod {modId}.");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting download link for Mod ID {modId}, File ID {fileId}: {ex.Message}");
                return null;
            }
        }


        public async Task<bool> DownloadFileAsync(string downloadUrl, string savePath, IProgress<double>? progress = null)
        {
            try
            {
                string targetDirectory = Path.GetDirectoryName(savePath)!;
                Directory.CreateDirectory(targetDirectory);

                Debug.WriteLine($"[Download] targetDirectory: {targetDirectory}");
                Debug.WriteLine($"[Download] savePath: {savePath}");

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progress != null;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        double percent = (double)totalRead / totalBytes * 100;
                        progress!.Report(percent);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to download file: {ex.Message}");
                return false;
            }
        }


        public async Task<List<ModFile>> GetModFilesAsync(string game, int modId)
        {
            string url = $"{BaseUrl}/games/{game}/mods/{modId}/files.json";
            Debug.WriteLine($"[NexusApiService] Fetching mod files for Mod ID {modId}");
            Debug.WriteLine($"[NexusApiService] Request URL: {url}");

            var modFiles = new List<ModFile>();

            try
            {
                var response = await GetAsync(url);
                if (response == null)
                {
                    Debug.WriteLine($"[NexusApiService] Response was null for Mod ID {modId}.");
                    return modFiles;
                }

                Debug.WriteLine($"[NexusApiService] HTTP Status Code: {response.StatusCode}");

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Debug.WriteLine($"[NexusApiService] Access denied for Mod ID {modId} (403 Forbidden).");
                    return modFiles;
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                if (!root.TryGetProperty("files", out var filesArray) || filesArray.ValueKind != JsonValueKind.Array)
                {
                    Debug.WriteLine($"[NexusApiService] 'files' property missing or not an array for Mod ID {modId}.");
                    return modFiles;
                }

                Debug.WriteLine($"[NexusApiService] Found {filesArray.GetArrayLength()} file entries.");

                foreach (var file in filesArray.EnumerateArray())
                {
                    try
                    {
                        int fileId = file.GetProperty("id").ValueKind switch
                        {
                            JsonValueKind.Number => file.GetProperty("id").GetInt32(),
                            JsonValueKind.Array => file.GetProperty("id")[0].GetInt32(),
                            _ => throw new InvalidOperationException("Invalid 'id' format")
                        };

                        string nameForDisplay = file.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                            ? (nameProp.GetString() ?? $"file_{fileId}")
                            : $"file_{fileId}";

                        string displayName = FileNameHelper.NormalizeDisplayFileName(nameForDisplay);


                        long sizeBytes = file.TryGetProperty("size_kb", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number
                            ? sizeProp.GetInt64() * 1024
                            : 0;

                        DateTime uploaded = file.TryGetProperty("uploaded_timestamp", out var tsProp) && tsProp.ValueKind == JsonValueKind.Number
                            ? DateTimeOffset.FromUnixTimeSeconds(tsProp.GetInt64()).UtcDateTime
                            : DateTime.MinValue;

                        string description = file.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                            ? descProp.GetString() ?? ""
                            : "";

                        Debug.WriteLine($"[NexusApiService] Parsed File -> ID: {fileId}, Name: {displayName}, Size: {sizeBytes} bytes, Uploaded: {uploaded}");

                        modFiles.Add(new ModFile
                        {
                            FileId = fileId,
                            FileName = displayName,
                            FileSizeBytes = sizeBytes,
                            UploadedTimestamp = uploaded,
                            Description = description
                        });
                    }
                    catch (Exception innerEx)
                    {
                        Debug.WriteLine($"[NexusApiService] Failed to parse file entry: {innerEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NexusApiService] Error fetching mod files for Mod ID {modId}: {ex.Message}");
            }

            return modFiles;
        }


        public async Task<List<Mod>> GetTrackedModsAsync(string game = "cyberpunk2077")
        {
            var url = $"{BaseUrl}/user/tracked_mods.json";
            var mods = new List<Mod>();

            try
            {
                Debug.WriteLine("Fetching tracked mods...");
                var response = await GetAsync(url);
                if (response == null) return mods;
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                var tasks = root.EnumerateArray()
                    .Where(entry => entry.TryGetProperty("domain_name", out var domainProp) &&
                                    domainProp.GetString() == game) // filter by domain name
                    .Select(async entry =>
                    {
                        if (entry.TryGetProperty("mod_id", out var modIdElement) &&
                            modIdElement.TryGetInt32(out int modId))
                        {
                            Debug.WriteLine($"Fetching details for Mod ID: {modId}");
                            var modDetails = await GetModDetailsAsync(game, modId);
                            if (modDetails != null)
                            {
                                lock (mods) mods.Add(modDetails);
                            }
                            else
                            {
                                Debug.WriteLine($"Mod details not found or failed to parse for Mod ID: {modId}");
                            }
                        }

                    });

                await Task.WhenAll(tasks);
                Debug.WriteLine($"Successfully fetched {mods.Count} tracked mods.");
                return mods;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching tracked mods: {ex.Message}");
                return mods;
            }
        }


        public async Task<Mod?> GetModDetailsAsync(string game, int modId)
        {
            var url = $"{BaseUrl}/games/{game}/mods/{modId}.json";

            try
            {
                var response = await GetAsync(url);
                if (response == null) return null;
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                var name = root.TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString() ?? "Unknown Name"
                    : "Unknown Name";

                var category = root.TryGetProperty("category_id", out var catProp)
                    ? CategoryHelper.GetCategoryName(game, catProp.GetInt32())
                    : "Unknown";

                return new Mod
                {
                    ModId = modId,
                    Name = name,
                    Category = category
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting details for Mod ID {modId}: {ex.Message}");
                return null;
            }
        }


        public async Task<bool> TrackModAsync(int modId, string game = "cyberpunk2077")
        {
            var url = $"{BaseUrl}/user/tracked_mods.json?domain_name={game}";
            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("mod_id", modId.ToString())
    });

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[INFO] Tracked mod {modId} successfully.");
                    return true;
                }

                Debug.WriteLine($"[WARN] Failed to track mod {modId}. Status: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error tracking mod {modId}: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> UntrackModAsync(int modId, string game = "cyberpunk2077")
        {
            var url = $"{BaseUrl}/user/tracked_mods.json?domain_name={game}";
            var content = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("mod_id", modId.ToString())
    });

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri(url),
                    Content = content
                };

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[INFO] Untracked mod {modId} successfully.");
                    return true;
                }

                Debug.WriteLine($"[WARN] Failed to untrack mod {modId}. Status: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error untracking mod {modId}: {ex.Message}");
            }

            return false;
        }


        public async Task<List<int>> GetTrackedModIdsAsync(string game = "cyberpunk2077")
        {
            var url = $"{BaseUrl}/user/tracked_mods.json";
            var modIds = new List<int>();

            try
            {
                var response = await GetAsync(url);
                if (response == null) return modIds;

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                foreach (var entry in root.EnumerateArray())
                {
                    if (entry.TryGetProperty("domain_name", out var domainProp) &&
                        domainProp.GetString() == game &&
                        entry.TryGetProperty("mod_id", out var modIdProp) &&
                        modIdProp.TryGetInt32(out int modId))
                    {
                        modIds.Add(modId);
                    }
                }

                return modIds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to fetch tracked mod IDs: {ex.Message}");
                return modIds;
            }
        }



    }
}
