using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DiscordActivityMockV2
{
    /// <summary>
    /// Removes files that earlier runs left behind. Runs in the background at startup and never deletes
    /// anything that is in use, newer than this version, or not created by this app.
    /// </summary>
    internal static class StartupCleanup
    {
        public const string SessionLockFileName = ".lock";

        /// <summary>A running installer may still use its download folder for a while.</summary>
        internal static readonly TimeSpan DownloadMaxAge = TimeSpan.FromHours(1);

        /// <summary>Sessions created before 2026.11.0 have no lock file, so age is the only safe signal.</summary>
        internal static readonly TimeSpan UnlockedSessionMaxAge = TimeSpan.FromHours(24);

        private static readonly Regex ExtractionFolderPattern = new(
            @"^DiscordActivityMock-v(?<version>[0-9A-Za-z.\-]+)-win-x86-Portable$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static void Run(string currentVersion, string? processPath, string sessionsRoot)
        {
            DateTime now = DateTime.UtcNow;
            string tempPath = Path.GetTempPath();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            Try("Clean orphaned sessions", () => CleanOrphanedSessions(sessionsRoot, now));
            Try("Clean downloaded updates", () => CleanDownloadedUpdates(tempPath, now));
            if (string.IsNullOrWhiteSpace(processPath))
                return;

            string executablePath = processPath;
            Try("Clean old portable updates and extractions", () =>
            {
                PortableRoot root = PortableLayout.Resolve(executablePath, localAppData);
                CleanOldPortableUpdates(executablePath, root, currentVersion);

                // The original executable stays the user's shortcut and extracts itself on every launch before it
                // redirects, so its extraction is kept. Without a known original folder, skip this cleanup.
                if (root.RootDirectory != null)
                    CleanOldExtractions(GetExtractionBases(tempPath), currentVersion, GetLauncherNames(root.RootDirectory));
            });
        }

        /// <summary>Held open with no sharing while the session exists, so other instances skip it.</summary>
        public static FileStream AcquireSessionLock(string sessionDirectory) =>
            new(Path.Combine(sessionDirectory, SessionLockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        internal static void CleanOrphanedSessions(string sessionsRoot, DateTime utcNow)
        {
            if (!Directory.Exists(sessionsRoot))
                return;

            foreach (string session in Directory.GetDirectories(sessionsRoot))
            {
                if (Guid.TryParseExact(Path.GetFileName(session), "N", out _) && CanDeleteSession(session, utcNow))
                    TryDeleteDirectory(session);
            }
        }

        internal static bool CanDeleteSession(string sessionDirectory, DateTime utcNow)
        {
            string lockPath = Path.Combine(sessionDirectory, SessionLockFileName);
            if (File.Exists(lockPath))
            {
                try
                {
                    using var probe = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    return true; // the instance that owned it has exited
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return false; // another instance is using this session
                }
            }

            return utcNow - Directory.GetLastWriteTimeUtc(sessionDirectory) > UnlockedSessionMaxAge;
        }

        internal static void CleanDownloadedUpdates(string tempPath, DateTime utcNow)
        {
            if (!Directory.Exists(tempPath))
                return;

            foreach (string directory in Directory.GetDirectories(tempPath, UpdateChecker.TemporaryDownloadPrefix + "*"))
            {
                if (utcNow - Directory.GetLastWriteTimeUtc(directory) > DownloadMaxAge)
                    TryDeleteDirectory(directory);
            }
        }

        internal static void CleanOldPortableUpdates(string processPath, PortableRoot root, string currentVersion)
        {
            string currentDirectory = Path.GetDirectoryName(Path.GetFullPath(processPath))!;
            foreach (string updatesDirectory in GetUpdatesDirectories(processPath, root))
            {
                foreach (string directory in FindObsoleteUpdateFolders(updatesDirectory, currentVersion, currentDirectory))
                {
                    if (!IsAnyExecutableRunning(directory))
                        TryDeleteDirectory(directory);
                }
            }
        }

        /// <summary>
        /// The root's updates folders plus every "updates" folder on the way from the running executable up to the
        /// root, because versions before 2026.11.0 nested each update inside the previous one (updates\a\updates\b).
        /// </summary>
        internal static List<string> GetUpdatesDirectories(string processPath, PortableRoot root)
        {
            var directories = new List<string>();
            void Add(string? candidate)
            {
                if (!string.IsNullOrWhiteSpace(candidate) && !directories.Any(existing => PathGuard.IsSamePath(existing, candidate)))
                    directories.Add(candidate);
            }

            Add(root.PreferredUpdatesDirectory);
            Add(root.FallbackUpdatesDirectory);

            string boundary = root.RootDirectory ?? root.FallbackUpdatesDirectory;
            for (string? directory = Path.GetDirectoryName(Path.GetFullPath(processPath));
                 directory != null && PathGuard.IsWithin(boundary, directory);
                 directory = Path.GetDirectoryName(directory))
            {
                Add(Path.Combine(directory, PortableLayout.UpdatesFolderName));
            }

            return directories;
        }

        /// <summary>Names (without .exe) of the portable executables in the folder the user starts the app from.</summary>
        internal static HashSet<string> GetLauncherNames(string? rootDirectory)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(rootDirectory) && Directory.Exists(rootDirectory))
            {
                foreach (string executable in Directory.GetFiles(rootDirectory, "DiscordActivityMock-v*-win-x86-Portable.exe"))
                    names.Add(Path.GetFileNameWithoutExtension(executable));
            }
            return names;
        }

        /// <summary>
        /// Update folders for versions older than <paramref name="currentVersion"/>, except the one
        /// that contains the running executable (older layouts nested updates inside updates).
        /// </summary>
        internal static List<string> FindObsoleteUpdateFolders(string? updatesDirectory, string currentVersion, string currentExecutableDirectory)
        {
            var obsolete = new List<string>();
            if (string.IsNullOrWhiteSpace(updatesDirectory) || !Directory.Exists(updatesDirectory))
                return obsolete;

            foreach (string directory in Directory.GetDirectories(updatesDirectory))
            {
                if (PortableLayout.TryParseUpdateFolderName(Path.GetFileName(directory), out string version) &&
                    AppVersion.IsNewer(currentVersion, version) &&
                    !PathGuard.IsSamePath(directory, currentExecutableDirectory) &&
                    !PathGuard.IsWithin(directory, currentExecutableDirectory))
                    obsolete.Add(directory);
            }

            return obsolete;
        }

        internal static IEnumerable<string> GetExtractionBases(string tempPath)
        {
            var bases = new List<string> { Path.Combine(tempPath, ".net") };
            string? custom = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR");
            if (!string.IsNullOrWhiteSpace(custom) && Path.IsPathFullyQualified(custom) &&
                !bases.Any(existing => PathGuard.IsSamePath(existing, custom)))
                bases.Add(custom);
            return bases;
        }

        /// <summary>
        /// Single-file releases extract to &lt;base&gt;\DiscordActivityMock-v&lt;version&gt;-win-x86-Portable.
        /// Folders named in <paramref name="keepNames"/> are kept even when older.
        /// </summary>
        internal static void CleanOldExtractions(IEnumerable<string> extractionBases, string currentVersion, ISet<string> keepNames)
        {
            foreach (string extractionBase in extractionBases)
            {
                if (!Directory.Exists(extractionBase))
                    continue;

                foreach (string directory in Directory.GetDirectories(extractionBase, "DiscordActivityMock-v*-win-x86-Portable"))
                {
                    string name = Path.GetFileName(directory);
                    if (TryParseExtractionFolderName(name, out string version) &&
                        AppVersion.IsNewer(currentVersion, version) &&
                        !keepNames.Contains(name) &&
                        !IsProcessRunning(name))
                        TryDeleteDirectory(directory);
                }
            }
        }

        internal static bool TryParseExtractionFolderName(string? folderName, out string version)
        {
            version = "";
            Match match = ExtractionFolderPattern.Match(folderName ?? "");
            if (!match.Success)
                return false;

            version = match.Groups["version"].Value;
            return true;
        }

        /// <summary>Checks nested folders too: older layouts kept newer updates inside older ones.</summary>
        private static bool IsAnyExecutableRunning(string directory) =>
            Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories)
                     .Any(executable => IsProcessRunning(Path.GetFileNameWithoutExtension(executable)));

        private static bool IsProcessRunning(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
                process.Dispose();
            return processes.Length > 0;
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still in use; a later start will try again.
            }
        }

        private static void Try(string operation, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppLog.Error(operation, ex);
            }
        }
    }
}
