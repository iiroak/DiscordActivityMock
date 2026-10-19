using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
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
        private const string CurrentVersion = "2.1.4";
        private const string GitHubApiUrl = "https://api.github.com/repos/iiroak/DiscordActivityMock/releases/latest";
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

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
                        if (asset.Name.Contains($"win-{platform}", StringComparison.OrdinalIgnoreCase) &&
                            asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.DownloadUrl;
                            break;
                        }
                    }

                    return (true, latestVersion, downloadUrl, release.HtmlUrl);
                }

                return (false, null, null, null);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // A repository without a published release has nothing to update to.
                return (false, null, null, null);
            }
            catch (Exception ex)
            {
                AppLog.Error("Check for updates", ex);
                return (false, null, null, null);
            }
        }

        private static string GetPlatform()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                _ => "unsupported"
            };
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            try
            {
                return Version.TryParse(NormalizeVersion(latest), out var latestVersion) &&
                       Version.TryParse(NormalizeVersion(current), out var currentVersion) &&
                       latestVersion > currentVersion;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeVersion(string value)
        {
            string[] parts = value.Split('.');
            return parts.Length switch
            {
                1 => value + ".0.0",
                2 => value + ".0",
                _ => value
            };
        }

        public static void OpenReleasePage(string url)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                    !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Error("Open release page", ex);
            }
        }

        public static async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            string? tempPath = null;
            try
            {
                if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
                    !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                    return null;

                tempPath = Path.Combine(Path.GetTempPath(), $"DiscordActivityMock_Update_{Guid.NewGuid():N}.exe");
                
                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                if (totalBytes > 500L * 1024 * 1024)
                    return null;
                var canReportProgress = totalBytes != -1 && progress != null;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    totalRead += bytesRead;
                    if (totalRead > 500L * 1024 * 1024)
                        throw new InvalidDataException("The update executable exceeds the allowed download size.");

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    if (canReportProgress)
                    {
                        progress!.Report((double)totalRead / totalBytes * 100);
                    }
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                AppLog.Error("Download update", ex);
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                return null;
            }
        }
    }
}
