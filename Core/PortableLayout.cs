using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DiscordActivityMockV2
{
    internal enum RedirectDecision
    {
        /// <summary>The marker points to this executable; start normally and keep the marker.</summary>
        Keep,
        /// <summary>The marker points to a newer copy; start it and exit.</summary>
        Redirect,
        /// <summary>The marker target is missing, older, or unknown; delete the marker and start normally.</summary>
        DeleteMarker,
        /// <summary>
        /// This executable is an update copy and nothing newer is recorded; point the marker at it so the
        /// original executable (of any version) opens this copy.
        /// </summary>
        PointToCurrent
    }

    /// <summary>
    /// Identifies a portable copy by the folder the user originally started it from.
    /// <paramref name="Key"/> names the redirect marker. <paramref name="RootDirectory"/> is that folder, or null when
    /// the app runs from the %LOCALAPPDATA% fallback and the folder is only known by its key. Updates go to
    /// <paramref name="PreferredUpdatesDirectory"/> (next to the original executable) or, when that folder is not
    /// writable, to <paramref name="FallbackUpdatesDirectory"/>. <paramref name="IsUpdateCopy"/> is true when the
    /// running executable is an in-app update rather than the original download.
    /// </summary>
    internal sealed record PortableRoot(
        string Key,
        string? RootDirectory,
        string? PreferredUpdatesDirectory,
        string FallbackUpdatesDirectory,
        bool IsUpdateCopy);

    /// <summary>
    /// Layout of in-app portable updates:
    /// &lt;root&gt;\updates\&lt;version&gt;-&lt;32 hex&gt;\ or
    /// %LOCALAPPDATA%\DiscordActivityMock\PortableUpdates\&lt;key&gt;\&lt;version&gt;-&lt;32 hex&gt;\.
    /// </summary>
    internal static class PortableLayout
    {
        public const string UpdatesFolderName = "updates";

        private static readonly Regex UpdateFolderPattern = new(
            @"^(?<version>[0-9A-Za-z.\-]+)-[0-9a-fA-F]{32}$",
            RegexOptions.CultureInvariant);

        public static string GetFallbackBase(string localAppData) =>
            Path.Combine(localAppData, "DiscordActivityMock", "PortableUpdates");

        public static string CreateUpdateFolderName(string safeVersion) => $"{safeVersion}-{Guid.NewGuid():N}";

        public static bool TryParseUpdateFolderName(string? folderName, out string version)
        {
            version = "";
            if (string.IsNullOrEmpty(folderName))
                return false;

            Match match = UpdateFolderPattern.Match(folderName);
            if (!match.Success)
                return false;

            version = match.Groups["version"].Value;
            return true;
        }

        /// <summary>
        /// Walks up from the executable folder while it is an update folder inside "updates".
        /// This also collapses the nested updates\a\updates\b folders created before version 2026.11.0.
        /// </summary>
        public static string GetRootDirectory(string processPath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            while (true)
            {
                string? updatesDirectory = Path.GetDirectoryName(directory);
                string? root = updatesDirectory == null ? null : Path.GetDirectoryName(updatesDirectory);
                if (root == null ||
                    !TryParseUpdateFolderName(Path.GetFileName(directory), out _) ||
                    !string.Equals(Path.GetFileName(updatesDirectory), UpdatesFolderName, StringComparison.OrdinalIgnoreCase))
                    return directory;

                directory = root;
            }
        }

        public static PortableRoot Resolve(string processPath, string localAppData)
        {
            string fallbackBase = GetFallbackBase(localAppData);
            string rootDirectory = GetRootDirectory(processPath);

            // Running from %LOCALAPPDATA%\...\PortableUpdates\<key>\<version>-<id>: keep using <key>.
            string? keyDirectory = Path.GetDirectoryName(rootDirectory);
            if (keyDirectory != null &&
                TryParseUpdateFolderName(Path.GetFileName(rootDirectory), out _) &&
                IsDirectoryKey(Path.GetFileName(keyDirectory)) &&
                PathGuard.IsSamePath(Path.GetDirectoryName(keyDirectory), fallbackBase))
            {
                return new PortableRoot(Path.GetFileName(keyDirectory).ToUpperInvariant(), null, null, keyDirectory, IsUpdateCopy: true);
            }

            string key = GetDirectoryKey(rootDirectory);
            string executableDirectory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            return new PortableRoot(
                key,
                rootDirectory,
                Path.Combine(rootDirectory, UpdatesFolderName),
                Path.Combine(fallbackBase, key),
                IsUpdateCopy: !PathGuard.IsSamePath(rootDirectory, executableDirectory));
        }

        /// <summary>Same key format that versions 2.2.4 to 2.2.8 used, so their markers stay valid.</summary>
        public static string GetDirectoryKey(string directory) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())))[..16];

        public static bool IsDirectoryKey(string? name) =>
            name is { Length: 16 } && name.All(char.IsAsciiHexDigit);

        /// <summary>
        /// Decides what to do with the redirect marker at startup. <paramref name="targetExecutablePath"/> is null when
        /// there is no marker. Versions 2.2.4 to 2.2.8 chained markers per update folder, so an update copy that finds
        /// an older or missing target takes over the marker instead of deleting it.
        /// </summary>
        public static RedirectDecision DecideRedirect(
            string currentVersion,
            string currentExecutablePath,
            string? targetExecutablePath,
            string? targetVersion,
            bool targetExists,
            bool isUpdateCopy)
        {
            if (targetExecutablePath != null && PathGuard.IsSamePath(currentExecutablePath, targetExecutablePath))
                return RedirectDecision.Keep;

            if (targetExecutablePath != null && targetExists && targetVersion != null &&
                AppVersion.IsNewer(targetVersion, currentVersion))
                return RedirectDecision.Redirect;

            return isUpdateCopy ? RedirectDecision.PointToCurrent : RedirectDecision.DeleteMarker;
        }
    }
}
