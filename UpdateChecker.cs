using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DiscordActivityMockV2
{
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";
        
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";
        
        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        
        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = "";
    }

    [JsonSerializable(typeof(GitHubRelease))]
    internal partial class UpdateJsonContext : JsonSerializerContext
    {
    }

    public static class UpdateChecker
    {
        private const string CurrentVersion = "2.0.0";
        private const string GitHubApiUrl = "https://api.github.com/repos/iiroak/DiscordActivityMock/releases/latest";
        private static readonly HttpClient _httpClient = new();

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DiscordActivityMock");
        }

        public static string GetCurrentVersion() => CurrentVersion;

        public static async Task<(bool hasUpdate, string? newVersion, string? downloadUrl, string? releaseUrl)> CheckForUpdateAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(GitHubApiUrl);
                var release = JsonSerializer.Deserialize(response, UpdateJsonContext.Default.GitHubRelease);

                if (release == null || string.IsNullOrEmpty(release.TagName))
                {
                    return (false, null, null, null);
                }

                var latestVersion = release.TagName.TrimStart('v', 'V');
                
                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    // Find the appropriate asset for the current platform
                    string? downloadUrl = null;
                    var platform = GetPlatform();
                    
                    foreach (var asset in release.Assets)
                    {
                        if (asset.Name.Contains(platform, StringComparison.OrdinalIgnoreCase) && 
                            asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.DownloadUrl;
                            break;
                        }
                    }

                    return (true, latestVersion, downloadUrl, release.HtmlUrl);
                }

                return (false, null, null, null);
            }
            catch
            {
                return (false, null, null, null);
            }
        }

        private static string GetPlatform()
        {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                var latestParts = latest.Split('.');
                var currentParts = current.Split('.');

                for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
                {
                    int latestNum = i < latestParts.Length ? int.Parse(latestParts[i]) : 0;
                    int currentNum = i < currentParts.Length ? int.Parse(currentParts[i]) : 0;

                    if (latestNum > currentNum) return true;
                    if (latestNum < currentNum) return false;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenReleasePage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public static async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "DiscordActivityMock_Update.zip");
                
                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && progress != null;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        progress!.Report((double)totalRead / totalBytes * 100);
                    }
                }

                return tempPath;
            }
            catch
            {
                return null;
            }
        }
    }
}
