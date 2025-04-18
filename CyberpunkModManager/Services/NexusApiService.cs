using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using CyberpunkModManager.Models;

namespace CyberpunkModManager.Services
{
    public class NexusApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://api.nexusmods.com/v1";
        private readonly string _apiKey;

        public NexusApiService(string apiKey)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Add("apikey", _apiKey);
        }

        private async Task<HttpResponseMessage?> GetWithRetryAsync(string url, int maxRetries = 3)
        {
            bool shownRateLimitMessage = false;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (!shownRateLimitMessage)
                    {
                        shownRateLimitMessage = true;
                        Console.WriteLine("[WARN] Rate limited (429). Waiting before retrying...");

                        // Notify the user
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                "Nexus Mods API rate limit reached. Retrying shortly...\n\nTry again later if this continues.",
                                "Rate Limit Triggered",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning
                            );
                        });
                    }

                    // Wait according to Retry-After header or fallback
                    if (response.Headers.TryGetValues("Retry-After", out var values))
                    {
                        if (int.TryParse(values.FirstOrDefault(), out int retryAfterSec))
                        {
                            await Task.Delay(TimeSpan.FromSeconds(retryAfterSec));
                        }
                        else
                        {
                            await Task.Delay(3000);
                        }
                    }
                    else
                    {
                        await Task.Delay(3000);
                    }

                    continue;
                }

                return response;
            }

            Console.WriteLine($"[ERROR] Failed after {maxRetries} attempts for URL: {url}");
            return null;
        }


        public async Task<bool> DownloadFileAsync(string downloadUrl, string savePath)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                var finalFileName = Path.GetFileName(savePath);

                // Try to get the file extension from Content-Disposition
                if (response.Content.Headers.ContentDisposition != null)
                {
                    var cdFileName = response.Content.Headers.ContentDisposition.FileName?.Trim('"');
                    if (!string.IsNullOrEmpty(cdFileName))
                    {
                        var ext = Path.GetExtension(cdFileName);
                        if (!string.IsNullOrEmpty(ext))
                        {
                            finalFileName = Path.ChangeExtension(finalFileName, ext);
                            savePath = Path.Combine(Path.GetDirectoryName(savePath)!, finalFileName);
                        }
                    }
                }
                else if (response.Content.Headers.ContentType?.MediaType is string mediaType)
                {
                    // Fallback using MIME type
                    if (mediaType == "application/zip")
                        savePath += ".zip";
                    else if (mediaType == "application/x-7z-compressed")
                        savePath += ".7z";
                    else if (mediaType == "application/x-rar-compressed")
                        savePath += ".rar";
                    else
                        savePath += ".zip"; // Safer default for archives
                }

                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!); // Just in case

                await using var fs = new FileStream(savePath, FileMode.Create);
                await response.Content.CopyToAsync(fs);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download failed: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Mod>> GetTrackedModsAsync(string game = "cyberpunk2077")
        {
            var url = $"{BaseUrl}/user/tracked_mods.json";
            var mods = new List<Mod>();

            try
            {
                Console.WriteLine("Fetching tracked mods...");
                var response = await GetWithRetryAsync(url);
                if (response == null) return mods;
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                var tasks = root.EnumerateArray().Select(async entry =>
                {
                    if (entry.TryGetProperty("mod_id", out var modIdElement) && modIdElement.TryGetInt32(out int modId))
                    {
                        Console.WriteLine($"Fetching details for Mod ID: {modId}");
                        var modDetails = await GetModDetailsAsync(game, modId);
                        if (modDetails != null)
                        {
                            lock (mods) mods.Add(modDetails);
                        }
                        else
                        {
                            Console.WriteLine($"Mod details not found or failed to parse for Mod ID: {modId}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("mod_id not found or invalid in tracked entry.");
                    }
                });

                await Task.WhenAll(tasks);
                Console.WriteLine($"Successfully fetched {mods.Count} tracked mods.");
                return mods;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching tracked mods: {ex.Message}");
                return mods;
            }
        }

        public async Task<Mod?> GetModDetailsAsync(string game, int modId)
        {
            var url = $"{BaseUrl}/games/{game}/mods/{modId}.json";

            try
            {
                var response = await GetWithRetryAsync(url);
                if (response == null) return null;
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Unknown Name" : "Unknown Name";
                var category = root.TryGetProperty("category_id", out var catProp) ? GetCategoryName(catProp.GetInt32()) : "Unknown";

                return new Mod
                {
                    ModId = modId,
                    Name = name,
                    Category = category
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting details for Mod ID {modId}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<ModFile>> GetModFilesAsync(string game, int modId)
        {
            var url = $"{BaseUrl}/games/{game}/mods/{modId}/files.json";
            Console.WriteLine($"[DEBUG] Fetching mod files for Mod ID: {modId}");
            Console.WriteLine($"[DEBUG] URL: {url}");

            try
            {
                var response = await GetWithRetryAsync(url);
                if (response == null) return new List<ModFile>();
                Console.WriteLine($"[DEBUG] Status Code: {response.StatusCode}");

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"[WARN] Mod ID {modId} is no longer accessible (403 Forbidden).");
                    return new List<ModFile>();
                }

                response.EnsureSuccessStatusCode(); // Only call if status is OK

                var json = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[DEBUG] Raw JSON Response: {json}");

                var root = JsonDocument.Parse(json).RootElement;
                var files = new List<ModFile>();

                if (root.TryGetProperty("files", out var filesArray) && filesArray.ValueKind == JsonValueKind.Array)
                {
                    Console.WriteLine($"[DEBUG] Found {filesArray.GetArrayLength()} file entries.");

                    foreach (var file in filesArray.EnumerateArray())
                    {
                        int fileId;
                        try
                        {
                            var idElement = file.GetProperty("id");
                            fileId = idElement.ValueKind switch
                            {
                                JsonValueKind.Number => idElement.GetInt32(),
                                JsonValueKind.Array when idElement.GetArrayLength() > 0 && idElement[0].ValueKind == JsonValueKind.Number => idElement[0].GetInt32(),
                                _ => throw new InvalidOperationException("Invalid file ID format")
                            };
                        }
                        catch (Exception idEx)
                        {
                            Console.WriteLine($"[WARN] Skipping file due to invalid ID: {idEx.Message}");
                            continue;
                        }

                        string fileName = file.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                            ? nameProp.GetString() ?? "Unknown"
                            : "Unknown";

                        long sizeBytes = file.TryGetProperty("size_kb", out var sizeProp) && sizeProp.ValueKind == JsonValueKind.Number
                            ? sizeProp.GetInt64() * 1024
                            : 0;

                        DateTime uploaded;
                        try
                        {
                            var timestampSeconds = file.GetProperty("uploaded_timestamp").GetInt64();
                            uploaded = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds).UtcDateTime;
                        }
                        catch
                        {
                            uploaded = DateTime.MinValue;
                            Console.WriteLine($"[WARN] Invalid uploaded_timestamp for file {fileId}");
                        }

                        string description = file.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                            ? descProp.GetString() ?? ""
                            : "";

                        Console.WriteLine($"[DEBUG] File ID: {fileId}, Name: {fileName}, Size: {sizeBytes}, Uploaded: {uploaded}");

                        files.Add(new ModFile
                        {
                            FileId = fileId,
                            FileName = fileName,
                            FileSizeBytes = sizeBytes,
                            UploadedTimestamp = uploaded,
                            Description = description
                        });
                    }
                }
                else
                {
                    Console.WriteLine("[DEBUG] 'files' array not found in JSON.");
                }

                return files;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to fetch mod files for Mod ID {modId}: {ex.Message}");
                return new List<ModFile>();
            }
        }





        public async Task<string?> GetDownloadLinkAsync(string game, int modId, int fileId)
        {
            var url = $"{BaseUrl}/games/{game}/mods/{modId}/files/{fileId}/download_link.json";

            try
            {
                var response = await GetWithRetryAsync(url);
                if (response == null) return null;
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var links = JsonDocument.Parse(json).RootElement;

                if (links.ValueKind == JsonValueKind.Array && links.GetArrayLength() > 0)
                {
                    return links[0].GetProperty("URI").GetString();
                }

                Console.WriteLine($"No download links returned for file {fileId} of mod {modId}.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting download link for Mod ID {modId}, File ID {fileId}: {ex.Message}");
                return null;
            }
        }

        private string GetCategoryName(int categoryId)
        {
            return categoryId switch
            {
                2 => "Miscellaneous",
                3 => "Armour and Clothing",
                4 => "Audio",
                5 => "Characters",
                6 => "Crafting",
                7 => "Gameplay",
                8 => "User Interface",
                9 => "Utilities",
                10 => "Visuals and Graphics",
                11 => "Weapons",
                12 => "Modders Resources",
                13 => "Appearance",
                14 => "Vehicles",
                15 => "Animations",
                16 => "Locations",
                17 => "Scripts",
                _ => "Unknown"
            };
        }
    }
}
