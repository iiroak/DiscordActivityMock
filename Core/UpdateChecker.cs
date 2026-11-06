using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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

        /// <summary>SHA-256 that GitHub computes at upload time, formatted as "sha256:&lt;64 hex&gt;".</summary>
        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }

    public sealed record UpdateInfo(
        string Version,
        string ReleaseUrl,
        GitHubAsset? PortableExe,
        GitHubAsset? PortableZip,
        GitHubAsset? Setup);

    public sealed record UpdateCheckResult(bool Succeeded, UpdateInfo? Update);

    /// <summary>
    /// <paramref name="FilePath"/> is set only when the download finished and its SHA-256 matched.
    /// <paramref name="IntegrityFailed"/> is true when the file could not be verified against GitHub's digest.
    /// </summary>
    public sealed record UpdateDownloadResult(string? FilePath, bool IntegrityFailed);

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
        public const string ReleasesPageUrl = "https://github.com/iiroak/DiscordActivityMock/releases/latest";
        internal const string TemporaryDownloadPrefix = "DiscordActivityMock_Update_";

        private const string GitHubApiUrl = "https://api.github.com/repos/iiroak/DiscordActivityMock/releases/latest";
        private const string ReleasesPath = "/iiroak/DiscordActivityMock/releases";
        private const string AssetDownloadPath = ReleasesPath + "/download";
        private const string Sha256DigestPrefix = "sha256:";
        private const long MaxUpdateDownloadBytes = 700L * 1024 * 1024;
        private const long MaxUpdateArchiveBytes = 1024L * 1024 * 1024;
        private const string InnoUninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{C2A64B4B-69FC-4F28-B048-5C4E661D4F32}_is1";
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly HttpClient _downloadClient = new() { Timeout = TimeSpan.FromMinutes(5) };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DiscordActivityMock");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            _downloadClient.DefaultRequestHeaders.Add("User-Agent", "DiscordActivityMock");
        }

        private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static string GetCurrentVersion() => AppVersion.Current;

        public static async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return new UpdateCheckResult(true, null);

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync(cancellationToken);
                return new UpdateCheckResult(true, ParseRelease(json, AppVersion.Current));
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

        /// <summary>Returns the update described by the release JSON, or null when it is not newer.</summary>
        internal static UpdateInfo? ParseRelease(string json, string currentVersion)
        {
            GitHubRelease? release = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.GitHubRelease);
            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                throw new JsonException("The release response does not contain a tag.");

            string latestVersion = AppVersion.Normalize(release.TagName);
            if (!AppVersion.IsNewer(latestVersion, currentVersion))
                return null;

            return new UpdateInfo(
                latestVersion,
                IsTrustedReleasePageUrl(release.HtmlUrl) ? release.HtmlUrl : ReleasesPageUrl,
                FindAsset(release, "-win-x86-Portable.exe"),
                FindAsset(release, "-win-x86-Portable.zip"),
                FindAsset(release, "-win-x86-Setup.exe"));
        }

        /// <summary>Only assets from this repository with a valid SHA-256 digest can be installed in-app.</summary>
        internal static GitHubAsset? FindAsset(GitHubRelease release, string suffix)
        {
            foreach (GitHubAsset? asset in release.Assets ?? Array.Empty<GitHubAsset>())
            {
                if (asset?.Name != null &&
                    asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    IsTrustedAssetUrl(asset.DownloadUrl) &&
                    TryParseSha256Digest(asset.Digest, out _))
                    return asset;
            }

            return null;
        }

        internal static bool TryParseSha256Digest(string? digest, out byte[] hash)
        {
            hash = Array.Empty<byte>();
            if (string.IsNullOrEmpty(digest) || !digest.StartsWith(Sha256DigestPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string hex = digest[Sha256DigestPrefix.Length..];
            if (hex.Length != 64 || !hex.All(char.IsAsciiHexDigit))
                return false;

            hash = Convert.FromHexString(hex);
            return true;
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
                    installLocation = Path.Combine(LocalAppData, "Programs", "DiscordActivityMock");

                return PathGuard.IsWithin(installLocation, Path.GetFullPath(processPath));
            }
            catch (Exception ex)
            {
                AppLog.Error("Detect installed package", ex);
                return false;
            }
        }

        public static async Task<UpdateDownloadResult> DownloadUpdateAsync(
            GitHubAsset asset,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            string? temporaryDirectory = null;
            try
            {
                if (!TryParseSha256Digest(asset.Digest, out byte[] expectedHash) ||
                    !IsTrustedAssetUrl(asset.DownloadUrl) ||
                    !Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out Uri? uri))
                {
                    AppLog.Warning("Download update", $"Refused to download '{asset.Name}' without a trusted URL and SHA-256 digest.");
                    return new UpdateDownloadResult(null, IntegrityFailed: true);
                }

                string fileName = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
                if (string.IsNullOrWhiteSpace(fileName) ||
                    fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    !(fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                      fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                    return new UpdateDownloadResult(null, IntegrityFailed: false);

                temporaryDirectory = Path.Combine(Path.GetTempPath(), TemporaryDownloadPrefix + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporaryDirectory);
                string temporaryPath = Path.Combine(temporaryDirectory, fileName);

                byte[] actualHash;
                using (var response = await _downloadClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    long? contentLength = response.Content.Headers.ContentLength;
                    if (contentLength > MaxUpdateDownloadBytes)
                        throw new InvalidDataException("The update package exceeds the allowed download size.");

                    await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = new FileStream(
                        temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    actualHash = await CopyAndHashAsync(
                        contentStream, fileStream, MaxUpdateDownloadBytes, contentLength ?? -1, progress, cancellationToken);
                }

                if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
                {
                    AppLog.Warning("Download update",
                        $"SHA-256 mismatch for '{fileName}': expected {Convert.ToHexString(expectedHash)}, got {Convert.ToHexString(actualHash)}.");
                    DeleteTemporaryDirectory(temporaryDirectory);
                    return new UpdateDownloadResult(null, IntegrityFailed: true);
                }

                return new UpdateDownloadResult(temporaryPath, IntegrityFailed: false);
            }
            catch (OperationCanceledException)
            {
                DeleteTemporaryDirectory(temporaryDirectory);
                return new UpdateDownloadResult(null, IntegrityFailed: false);
            }
            catch (Exception ex)
            {
                AppLog.Error("Download update", ex);
                DeleteTemporaryDirectory(temporaryDirectory);
                return new UpdateDownloadResult(null, IntegrityFailed: false);
            }
        }

        /// <summary>
        /// Copies <paramref name="source"/> to <paramref name="destination"/>, returns its SHA-256, and reports
        /// whole percentages only when they change.
        /// </summary>
        internal static async Task<byte[]> CopyAndHashAsync(
            Stream source,
            Stream destination,
            long maxBytes,
            long totalBytes,
            IProgress<int>? progress,
            CancellationToken cancellationToken)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long totalRead = 0;
            int lastReportedPercent = -1;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > maxBytes)
                    throw new InvalidDataException("The update package exceeds the allowed download size.");

                hash.AppendData(buffer, 0, bytesRead);
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                if (progress != null && totalBytes > 0)
                {
                    int percent = (int)Math.Min(100, totalRead * 100 / totalBytes);
                    if (percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        progress.Report(percent);
                    }
                }
            }

            await destination.FlushAsync(cancellationToken);
            return hash.GetHashAndReset();
        }

        public static string CreatePortableUpdateDirectory(string currentExecutablePath, string version)
        {
            string updateFolder = PortableLayout.CreateUpdateFolderName(SanitizeVersionForPath(version));
            PortableRoot root = PortableLayout.Resolve(currentExecutablePath, LocalAppData);

            if (root.PreferredUpdatesDirectory != null)
            {
                try
                {
                    string preferredDirectory = Path.Combine(root.PreferredUpdatesDirectory, updateFolder);
                    Directory.CreateDirectory(preferredDirectory);
                    return preferredDirectory;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // The portable folder is read-only; fall back to the user's local app data.
                }
            }

            string fallbackDirectory = Path.Combine(root.FallbackUpdatesDirectory, updateFolder);
            Directory.CreateDirectory(fallbackDirectory);
            return fallbackDirectory;
        }

        /// <summary>Deletes a folder created by <see cref="CreatePortableUpdateDirectory"/> after a failed update.</summary>
        public static void DeletePortableUpdateDirectory(string directory)
        {
            try
            {
                string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
                string? parentName = Path.GetFileName(Path.GetDirectoryName(fullPath));
                if (Directory.Exists(fullPath) &&
                    PortableLayout.TryParseUpdateFolderName(Path.GetFileName(fullPath), out _) &&
                    (string.Equals(parentName, PortableLayout.UpdatesFolderName, StringComparison.OrdinalIgnoreCase) ||
                     PortableLayout.IsDirectoryKey(parentName)))
                    Directory.Delete(fullPath, recursive: true);
            }
            catch (Exception ex)
            {
                AppLog.Error("Clean failed portable update", ex);
            }
        }

        public static string ExtractPortableZip(string archivePath, string destinationDirectory, string expectedExecutableName)
        {
            Directory.CreateDirectory(destinationDirectory);
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > 2000 || archive.Entries.Sum(entry => entry.Length) > MaxUpdateArchiveBytes)
                    throw new InvalidDataException("The portable update ZIP contains an unexpected amount of data.");

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
                    if (!PathGuard.IsWithin(destinationDirectory, destination))
                        throw new InvalidDataException("The portable update ZIP contains an invalid path.");
                }
            }

            ZipFile.ExtractToDirectory(archivePath, destinationDirectory);
            string executablePath = Path.Combine(destinationDirectory, expectedExecutableName);
            if (!File.Exists(executablePath))
                throw new FileNotFoundException("The portable ZIP does not contain its application executable.", executablePath);
            return executablePath;
        }

        /// <summary>
        /// The marker holds only the target path, the format that versions 2.2.4 to 2.2.8 read, so an old
        /// executable started from its original folder still opens the newest update.
        /// </summary>
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

        /// <summary>Starts the newer portable copy recorded for this folder. Returns true when the app should exit.</summary>
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
                bool targetExists = Path.IsPathFullyQualified(targetExecutablePath) && File.Exists(targetExecutablePath);
                string? targetVersion = targetExists ? ReadExecutableVersion(targetExecutablePath) : null;

                switch (PortableLayout.DecideRedirect(AppVersion.Current, currentExecutablePath, targetExecutablePath, targetVersion, targetExists))
                {
                    case RedirectDecision.Redirect:
                        using (Process? process = Process.Start(new ProcessStartInfo
                        {
                            FileName = targetExecutablePath,
                            WorkingDirectory = Path.GetDirectoryName(targetExecutablePath)!,
                            UseShellExecute = true
                        }))
                        {
                            return process != null;
                        }

                    case RedirectDecision.DeleteMarker:
                        File.Delete(markerPath);
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Redirect to updated portable application", ex);
                return false;
            }
        }

        /// <summary>Reads the version stored in the executable's version resource.</summary>
        internal static string? ReadExecutableVersion(string executablePath)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);
                if (AppVersion.TryParse(info.ProductVersion, out _))
                    return AppVersion.Normalize(info.ProductVersion);

                if (info.FileMajorPart > 0 || info.FileMinorPart > 0 || info.FileBuildPart > 0)
                    return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
                return null;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                return null;
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
            string key = PortableLayout.Resolve(processPath, LocalAppData).Key;
            return Path.Combine(LocalAppData, "DiscordActivityMock", "PortableRedirects", key + ".txt");
        }

        private static void DeleteTemporaryDirectory(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (Directory.Exists(path) && Path.GetFileName(path).StartsWith(TemporaryDownloadPrefix, StringComparison.OrdinalIgnoreCase))
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

        /// <summary>A release asset of this repository: https://github.com/iiroak/DiscordActivityMock/releases/download/...</summary>
        internal static bool IsTrustedAssetUrl([NotNullWhen(true)] string? url) => IsTrustedGitHubUrl(url, AssetDownloadPath);

        /// <summary>A release page of this repository: https://github.com/iiroak/DiscordActivityMock/releases/...</summary>
        internal static bool IsTrustedReleasePageUrl([NotNullWhen(true)] string? url) => IsTrustedGitHubUrl(url, ReleasesPath);

        private static bool IsTrustedGitHubUrl([NotNullWhen(true)] string? url, string pathPrefix) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.IsDefaultPort &&
            string.IsNullOrEmpty(uri.UserInfo) &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            (uri.AbsolutePath.Equals(pathPrefix, StringComparison.OrdinalIgnoreCase) ||
             uri.AbsolutePath.StartsWith(pathPrefix + "/", StringComparison.OrdinalIgnoreCase));

        public static void OpenReleasePage(string? url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = IsTrustedReleasePageUrl(url) ? url : ReleasesPageUrl,
                    UseShellExecute = true
                })?.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.Error("Open release page", ex);
            }
        }
    }
}
