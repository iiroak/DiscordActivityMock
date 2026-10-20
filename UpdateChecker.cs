using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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

    public sealed record UpdateInfo(
        string Version,
        string ReleaseUrl,
        GitHubAsset? PortableExe,
        GitHubAsset? PortableZip,
        GitHubAsset? Setup);

    public sealed record UpdateCheckResult(bool Succeeded, UpdateInfo? Update);

    public enum UpdatePackageKind
    {
        PortableExe,
        PortableFolder,
        Installed
    }

    [JsonSerializable(typeof(GitHubRelease))]
    internal partial class UpdateJsonContext : JsonSerializerContext
    {
    }

    public static class UpdateChecker
    {
        private const string CurrentVersion = "2.2.7";
        private const string GitHubApiUrl = "https://api.github.com/repos/iiroak/DiscordActivityMock/releases/latest";
        private const long MaxUpdateDownloadBytes = 700L * 1024 * 1024;
        private const long MaxUpdateArchiveBytes = 1024L * 1024 * 1024;
        private const string InnoUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{C2A64B4B-69FC-4F28-B048-5C4E661D4F32}_is1";
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly HttpClient _downloadClient = new() { Timeout = TimeSpan.FromMinutes(5) };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DiscordActivityMock");
            _downloadClient.DefaultRequestHeaders.Add("User-Agent", "DiscordActivityMock");
        }

        public static string GetCurrentVersion() => CurrentVersion;

        public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new UpdateCheckResult(true, null);

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                var release = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.GitHubRelease);
                if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                    return new UpdateCheckResult(false, null);

                string latestVersion = release.TagName.TrimStart('v', 'V');
                if (!IsNewerVersion(latestVersion, CurrentVersion))
                    return new UpdateCheckResult(true, null);

                return new UpdateCheckResult(true, new UpdateInfo(
                    latestVersion,
                    release.HtmlUrl,
                    FindAsset(release, "-win-x86-Portable.exe"),
                    FindAsset(release, "-win-x86-Portable.zip"),
                    FindAsset(release, "-win-x86-Setup.exe")));
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(true, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new UpdateCheckResult(false, null);
            }
            catch (Exception ex)
            {
                AppLog.Error("Check for updates", ex);
                return new UpdateCheckResult(false, null);
            }
        }

        private static GitHubAsset? FindAsset(GitHubRelease release, string suffix)
        {
            foreach (GitHubAsset asset in release.Assets)
            {
                if (asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && IsTrustedGitHubUrl(asset.DownloadUrl))
                    return asset;
            }

            return null;
        }

        public static UpdatePackageKind GetCurrentPackageKind()
        {
            string? processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
                return UpdatePackageKind.PortableExe;

            if (IsInstalledExecutable(processPath))
                return UpdatePackageKind.Installed;

            string executableDirectory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            string assemblyName = Path.GetFileNameWithoutExtension(processPath);
            if (File.Exists(Path.Combine(executableDirectory, assemblyName + ".dll")) ||
                File.Exists(Path.ChangeExtension(processPath, ".deps.json")))
                return UpdatePackageKind.PortableFolder;

            return UpdatePackageKind.PortableExe;
        }

        public static GitHubAsset? GetUpdateAsset(UpdateInfo update, UpdatePackageKind packageKind) => packageKind switch
        {
            UpdatePackageKind.Installed => update.Setup,
            UpdatePackageKind.PortableFolder => update.PortableZip,
            _ => update.PortableExe
        };

        private static bool IsInstalledExecutable(string processPath)
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InnoUninstallKey);
                string? installLocation = key?.GetValue("InstallLocation") as string;
                if (string.IsNullOrWhiteSpace(installLocation))
                    installLocation = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs", "DiscordActivityMock");

                string executable = Path.GetFullPath(processPath);
                string installRoot = Path.GetFullPath(installLocation).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return executable.StartsWith(installRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                AppLog.Error("Detect installed package", ex);
                return false;
            }
        }

        public static async Task<string?> DownloadUpdateAsync(
            string downloadUrl,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? temporaryDirectory = null;
            try
            {
                if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out Uri? uri) || !IsTrustedGitHubUrl(uri.AbsoluteUri))
                    return null;

                string fileName = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
                if (string.IsNullOrWhiteSpace(fileName) ||
                    fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    !(fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                      fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                    return null;

                temporaryDirectory = Path.Combine(Path.GetTempPath(), $"DiscordActivityMock_Update_{Guid.NewGuid():N}");
                Directory.CreateDirectory(temporaryDirectory);
                string temporaryPath = Path.Combine(temporaryDirectory, fileName);

                using var response = await _downloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long contentLength && contentLength > MaxUpdateDownloadBytes)
                    throw new InvalidDataException("The update package exceeds the allowed download size.");

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    totalRead += bytesRead;
                    if (totalRead > MaxUpdateDownloadBytes)
                        throw new InvalidDataException("The update package exceeds the allowed download size.");

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    if (totalBytes > 0)
                        progress?.Report((double)totalRead / totalBytes * 100);
                }

                await fileStream.FlushAsync(cancellationToken);
                return temporaryPath;
            }
            catch (OperationCanceledException)
            {
                DeleteTemporaryDirectory(temporaryDirectory);
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Error("Download update", ex);
                DeleteTemporaryDirectory(temporaryDirectory);
                return null;
            }
        }

        public static string CreatePortableUpdateDirectory(string currentExecutablePath, string version)
        {
            string safeVersion = SanitizeVersionForPath(version);
            string updateFolder = $"{safeVersion}-{Guid.NewGuid():N}";
            string currentDirectory = Path.GetDirectoryName(Path.GetFullPath(currentExecutablePath))!;

            try
            {
                string preferredDirectory = Path.Combine(currentDirectory, "updates", updateFolder);
                Directory.CreateDirectory(preferredDirectory);
                return preferredDirectory;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                string currentDirectoryHash = GetDirectoryHash(currentDirectory);
                string fallbackDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DiscordActivityMock", "PortableUpdates", currentDirectoryHash, updateFolder);
                Directory.CreateDirectory(fallbackDirectory);
                return fallbackDirectory;
            }
        }

        public static string ExtractPortableZip(string archivePath, string destinationDirectory, string expectedExecutableName)
        {
            Directory.CreateDirectory(destinationDirectory);
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > 2000 || archive.Entries.Sum(entry => entry.Length) > MaxUpdateArchiveBytes)
                    throw new InvalidDataException("The portable update ZIP contains an unexpected amount of data.");

                string extractionRoot = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
                    if (!destination.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The portable update ZIP contains an invalid path.");
                }
            }

            ZipFile.ExtractToDirectory(archivePath, destinationDirectory);
            string executablePath = Path.Combine(destinationDirectory, expectedExecutableName);
            if (!File.Exists(executablePath))
                throw new FileNotFoundException("The portable ZIP does not contain its application executable.", executablePath);
            return executablePath;
        }

        public static void SavePortableUpdateRedirect(string currentExecutablePath, string targetExecutablePath)
        {
            string markerPath = GetPortableRedirectMarkerPath(currentExecutablePath);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            string temporaryMarker = markerPath + ".tmp";
            File.WriteAllText(temporaryMarker, Path.GetFullPath(targetExecutablePath));
            File.Move(temporaryMarker, markerPath, overwrite: true);
        }

        public static void RemovePortableUpdateRedirect(string currentExecutablePath)
        {
            try
            {
                string markerPath = GetPortableRedirectMarkerPath(currentExecutablePath);
                if (File.Exists(markerPath))
                    File.Delete(markerPath);
            }
            catch (Exception ex)
            {
                AppLog.Error("Remove portable update redirect", ex);
            }
        }

        public static bool TryRedirectPortableUpdate()
        {
            string? currentExecutablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExecutablePath) || GetCurrentPackageKind() == UpdatePackageKind.Installed)
                return false;

            try
            {
                string markerPath = GetPortableRedirectMarkerPath(currentExecutablePath);
                if (!File.Exists(markerPath))
                    return false;

                string targetExecutablePath = File.ReadAllText(markerPath).Trim();
                if (!File.Exists(targetExecutablePath) ||
                    string.Equals(Path.GetFullPath(currentExecutablePath), Path.GetFullPath(targetExecutablePath), StringComparison.OrdinalIgnoreCase))
                    return false;

                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExecutablePath,
                    WorkingDirectory = Path.GetDirectoryName(targetExecutablePath)!,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("Redirect to updated portable application", ex);
                return false;
            }
        }

        public static void DeleteDownloadedUpdate(string path)
        {
            DeleteTemporaryDirectory(Path.GetDirectoryName(path));
        }

        public static string GetPortableExecutableName(string version) =>
            $"DiscordActivityMock-v{SanitizeVersionForPath(version)}-win-x86-Portable.exe";

        private static string GetPortableRedirectMarkerPath(string processPath)
        {
            string processDirectory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            string key = GetDirectoryHash(processDirectory);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DiscordActivityMock", "PortableRedirects", key + ".txt");
        }

        private static string GetDirectoryHash(string directory) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())))[..16];

        private static void DeleteTemporaryDirectory(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (Directory.Exists(path) && Path.GetFileName(path).StartsWith("DiscordActivityMock_Update_", StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(path, recursive: true);
            }
            catch (Exception ex)
            {
                AppLog.Error("Clean downloaded update", ex);
            }
        }

        private static string SanitizeVersionForPath(string version)
        {
            string safe = new(version.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-').ToArray());
            if (string.IsNullOrWhiteSpace(safe))
                throw new InvalidDataException("The release version is invalid.");
            return safe;
        }

        private static bool IsTrustedGitHubUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);

        private static bool IsNewerVersion(string latest, string current)
        {
            string Normalize(string value)
            {
                string[] parts = value.Split('.');
                return parts.Length switch
                {
                    1 => value + ".0.0",
                    2 => value + ".0",
                    _ => value
                };
            }

            return Version.TryParse(Normalize(latest), out Version? latestVersion) &&
                   Version.TryParse(Normalize(current), out Version? currentVersion) &&
                   latestVersion > currentVersion;
        }

        public static void OpenReleasePage(string url)
        {
            try
            {
                if (!IsTrustedGitHubUrl(url))
                    return;

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Error("Open release page", ex);
            }
        }
    }
}
