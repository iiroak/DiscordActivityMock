using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DiscordActivityMockV2
{
    public sealed class SteamManifestItem
    {
        public string appId { get; set; } = "";
        public string name { get; set; } = "";
        public string stateFlags { get; set; } = "4";
        public string installDir { get; set; } = "";
    }

    internal sealed record SteamManifestLaunch(string ExecutablePath, string WorkingDirectory, string ManifestPath);

    internal static class SteamManifestSupport
    {
        public static void Validate(SteamManifestItem manifest, string activityFolder)
        {
            manifest.appId = manifest.appId.Trim();
            manifest.name = manifest.name.Trim();
            manifest.stateFlags = manifest.stateFlags.Trim();
            manifest.installDir = manifest.installDir.Trim();

            if (!uint.TryParse(manifest.appId, out _))
                throw new ArgumentException("The Steam manifest AppID must be a positive number.");
            if (string.IsNullOrWhiteSpace(manifest.name) || manifest.name.Any(char.IsControl))
                throw new ArgumentException("The Steam manifest name is invalid.");
            if (!uint.TryParse(manifest.stateFlags, out _))
                throw new ArgumentException("The Steam manifest StateFlags value must be a number.");

            ValidateInstallDirectory(manifest.installDir);
            if (!string.Equals(activityFolder, manifest.installDir, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("For a Steam manifest entry, folder must match steamManifest.installDir.");
        }

        public static SteamManifestLaunch Prepare(
            SteamManifestItem manifest,
            string sourceFolder,
            string sourceExecutable,
            string targetExecutableName,
            IEnumerable<string>? steamLibrariesOverride = null)
        {
            Validate(manifest, manifest.installDir);
            if (!Directory.Exists(sourceFolder) || !File.Exists(sourceExecutable))
                throw new FileNotFoundException("The temporary activity executable is not available to copy.", sourceExecutable);

            List<string> libraries = (steamLibrariesOverride ?? FindSteamLibraries()).ToList();
            if (libraries.Count == 0)
                throw new DirectoryNotFoundException("Could not find a Steam library. Start Steam once and verify its installation path.");

            string expectedManifest = BuildManifest(manifest);
            string manifestFileName = $"appmanifest_{manifest.appId}.acf";
            string[] librariesWithManifest = libraries
                .Where(library => File.Exists(Path.Combine(library, "steamapps", manifestFileName)))
                .ToArray();
            if (librariesWithManifest.Length > 1)
                throw new IOException($"More than one Steam library already contains {manifestFileName}; no files were changed.");
            if (librariesWithManifest.Length == 1)
                libraries = librariesWithManifest.ToList();

            UnauthorizedAccessException? accessError = null;
            foreach (string library in libraries)
            {
                string steamAppsPath = Path.Combine(library, "steamapps");
                string commonPath = Path.Combine(steamAppsPath, "common");
                string manifestPath = Path.Combine(steamAppsPath, manifestFileName);
                string gameDirectory = Path.Combine(commonPath, manifest.installDir);
                string executablePath = Path.Combine(gameDirectory, targetExecutableName + ".exe");

                bool manifestExists = File.Exists(manifestPath);
                if (manifestExists && !ManifestMatches(manifestPath, manifest))
                    throw new IOException($"Steam already has a different {manifestFileName}; it was left untouched.");
                if (Directory.Exists(manifestPath))
                    throw new IOException($"A directory already exists where Steam expects {manifestFileName}; it was left untouched.");

                bool gameDirectoryExists = Directory.Exists(gameDirectory);
                bool reusableDirectory = gameDirectoryExists && ContainsOnlyMatchingMockFiles(
                    gameDirectory, sourceFolder, sourceExecutable, targetExecutableName);
                if (gameDirectoryExists && !reusableDirectory)
                {
                    if (manifestExists)
                        throw new IOException($"The Steam library already contains {manifest.installDir}. Existing game files were left untouched.");

                    continue;
                }
                if (File.Exists(gameDirectory))
                    continue;

                bool createdGameDirectory = false;
                var createdFiles = new List<string>();
                var createdDirectories = new List<string>();
                var createdDirectorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool createdManifest = false;
                try
                {
                    if (!gameDirectoryExists)
                    {
                        Directory.CreateDirectory(gameDirectory);
                        createdGameDirectory = true;
                    }

                    foreach (string sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
                    {
                        string destinationRelativePath = GetDestinationRelativePath(
                            sourceFolder, sourceExecutable, targetExecutableName, sourceFile);
                        string destinationFile = Path.Combine(gameDirectory, destinationRelativePath);

                        if (File.Exists(destinationFile))
                        {
                            if (!FilesMatch(sourceFile, destinationFile))
                                throw new IOException($"The existing Steam file '{destinationRelativePath}' does not match the temporary mock executable and was left untouched.");
                            continue;
                        }

                        string destinationDirectory = Path.GetDirectoryName(destinationFile)!;
                        if (!Directory.Exists(destinationDirectory))
                        {
                            var missingDirectories = new List<string>();
                            for (string? currentDirectory = destinationDirectory;
                                 !string.IsNullOrEmpty(currentDirectory) && !Directory.Exists(currentDirectory);
                                 currentDirectory = Path.GetDirectoryName(currentDirectory))
                            {
                                missingDirectories.Add(currentDirectory);
                                if (string.Equals(Path.GetDirectoryName(currentDirectory), gameDirectory, StringComparison.OrdinalIgnoreCase))
                                    break;
                            }

                            Directory.CreateDirectory(destinationDirectory);
                            foreach (string createdDirectory in missingDirectories)
                            {
                                if (createdDirectorySet.Add(createdDirectory))
                                    createdDirectories.Add(createdDirectory);
                            }
                        }

                        createdFiles.Add(destinationFile);
                        File.Copy(sourceFile, destinationFile, overwrite: false);
                    }

                    if (!manifestExists)
                    {
                        using var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                        createdManifest = true;
                        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                        writer.Write(expectedManifest);
                    }

                    return new SteamManifestLaunch(executablePath, gameDirectory, manifestPath);
                }
                catch (UnauthorizedAccessException ex)
                {
                    accessError = ex;
                    if (createdManifest)
                    {
                        try { File.Delete(manifestPath); } catch { }
                    }
                    foreach (string createdFile in createdFiles)
                    {
                        try { File.Delete(createdFile); } catch { }
                    }
                    foreach (string createdDirectory in createdDirectories)
                    {
                        try { Directory.Delete(createdDirectory, recursive: false); } catch { }
                    }
                    if (createdGameDirectory)
                    {
                        try { Directory.Delete(gameDirectory, recursive: true); } catch { }
                    }
                }
                catch
                {
                    if (createdManifest)
                    {
                        try { File.Delete(manifestPath); } catch { }
                    }
                    foreach (string createdFile in createdFiles)
                    {
                        try { File.Delete(createdFile); } catch { }
                    }
                    foreach (string createdDirectory in createdDirectories)
                    {
                        try { Directory.Delete(createdDirectory, recursive: false); } catch { }
                    }
                    if (createdGameDirectory)
                    {
                        try { Directory.Delete(gameDirectory, recursive: true); } catch { }
                    }
                    throw;
                }
            }

            if (accessError != null)
                throw new UnauthorizedAccessException("Windows denied writing the mock executable or Steam manifest to every available library. Choose a writable Steam library or grant the app permission.", accessError);

            throw new IOException($"No Steam library is available for '{manifest.installDir}'. A folder with that name already exists in each candidate library, so nothing was overwritten.");
        }

        internal static string BuildManifest(SteamManifestItem manifest)
        {
            string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);

            return string.Join(Environment.NewLine, new[]
            {
                "\"AppState\"",
                "{",
                $"\t\"appid\"\t\t\"{Escape(manifest.appId)}\"",
                $"\t\"name\"\t\t\"{Escape(manifest.name)}\"",
                $"\t\"StateFlags\"\t\"{Escape(manifest.stateFlags)}\"",
                $"\t\"installdir\"\t\"{Escape(manifest.installDir)}\"",
                "}",
                ""
            });
        }

        private static void ValidateInstallDirectory(string installDir)
        {
            if (string.IsNullOrWhiteSpace(installDir) || installDir is "." or ".." ||
                installDir.Contains('/') || installDir.Contains('\\') ||
                installDir.EndsWith(' ') || installDir.EndsWith('.') ||
                installDir.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("The Steam manifest installDir must be one valid folder name.");
        }

        private static bool ManifestMatches(string path, SteamManifestItem manifest)
        {
            string content = File.ReadAllText(path);
            return string.Equals(ReadVdfValue(content, "appid"), manifest.appId, StringComparison.Ordinal) &&
                   string.Equals(ReadVdfValue(content, "name"), manifest.name, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(ReadVdfValue(content, "installdir"), manifest.installDir, StringComparison.OrdinalIgnoreCase);
        }

        private static string? ReadVdfValue(string content, string key)
        {
            foreach (string line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string[] parts = line.Trim().Split('"');
                if (parts.Length >= 4 && string.Equals(parts[1], key, StringComparison.OrdinalIgnoreCase))
                    return parts[3].Replace("\\\"", "\"", StringComparison.Ordinal)
                                    .Replace("\\\\", "\\", StringComparison.Ordinal);
            }
            return null;
        }

        private static bool ContainsOnlyMatchingMockFiles(
            string destinationFolder,
            string sourceFolder,
            string sourceExecutable,
            string targetExecutableName)
        {
            string[] sourceFiles = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
            var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var expectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourceFile in sourceFiles)
            {
                string destinationRelativePath = GetDestinationRelativePath(
                    sourceFolder, sourceExecutable, targetExecutableName, sourceFile);
                if (!expected.TryAdd(destinationRelativePath, sourceFile))
                    return false;

                string? relativeDirectory = Path.GetDirectoryName(destinationRelativePath);
                while (!string.IsNullOrEmpty(relativeDirectory))
                {
                    expectedDirectories.Add(relativeDirectory);
                    relativeDirectory = Path.GetDirectoryName(relativeDirectory);
                }
            }

            foreach (string destinationDirectory in Directory.GetDirectories(destinationFolder, "*", SearchOption.AllDirectories))
            {
                string relativeDirectory = Path.GetRelativePath(destinationFolder, destinationDirectory);
                if (!expectedDirectories.Contains(relativeDirectory))
                    return false;
            }

            string[] destinationFiles = Directory.GetFiles(destinationFolder, "*", SearchOption.AllDirectories);
            if (destinationFiles.Length > expected.Count)
                return false;

            foreach (string destinationFile in destinationFiles)
            {
                string relativePath = Path.GetRelativePath(destinationFolder, destinationFile);
                if (!expected.TryGetValue(relativePath, out string? sourceFile) ||
                    !FilesMatch(sourceFile, destinationFile))
                    return false;
            }
            return true;
        }

        private static string GetDestinationRelativePath(
            string sourceFolder,
            string sourceExecutable,
            string targetExecutableName,
            string sourceFile)
        {
            if (string.Equals(sourceFile, sourceExecutable, StringComparison.OrdinalIgnoreCase))
                return targetExecutableName + ".exe";

            string relativePath = Path.GetRelativePath(sourceFolder, sourceFile);
            string expectedMuiName = Path.GetFileName(sourceExecutable) + ".mui";
            if (!string.Equals(Path.GetFileName(relativePath), expectedMuiName, StringComparison.OrdinalIgnoreCase))
                return relativePath;

            string? relativeDirectory = Path.GetDirectoryName(relativePath);
            string localizedFileName = targetExecutableName + ".exe.mui";
            return string.IsNullOrEmpty(relativeDirectory)
                ? localizedFileName
                : Path.Combine(relativeDirectory, localizedFileName);
        }

        private static bool FilesMatch(string leftPath, string rightPath)
        {
            var leftInfo = new FileInfo(leftPath);
            var rightInfo = new FileInfo(rightPath);
            if (!leftInfo.Exists || !rightInfo.Exists || leftInfo.Length != rightInfo.Length)
                return false;

            using var left = File.OpenRead(leftPath);
            using var right = File.OpenRead(rightPath);
            return SHA256.HashData(left).SequenceEqual(SHA256.HashData(right));
        }

        private static List<string> FindSteamLibraries()
        {
            var roots = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddRoot(string? value, bool escapedVdfPath = false)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;
                string path = value.Trim();
                if (escapedVdfPath)
                    path = path.Replace("\\\\", "\\", StringComparison.Ordinal);
                path = path.Replace('/', Path.DirectorySeparatorChar);
                try
                {
                    if (!Path.IsPathRooted(path))
                        return;
                    path = Path.GetFullPath(path);
                    if (Directory.Exists(Path.Combine(path, "steamapps")) && seen.Add(path))
                        roots.Add(path);
                }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
                catch (PathTooLongException) { }
            }

            foreach (string value in ReadSteamRegistryPaths())
                AddRoot(value);

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
                AddRoot(Path.Combine(programFilesX86, "Steam"));

            foreach (string root in roots.ToArray())
            {
                string libraryFoldersFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFoldersFile))
                    continue;

                foreach (string line in File.ReadLines(libraryFoldersFile))
                {
                    string[] parts = line.Trim().Split('"');
                    if (parts.Length >= 4 && string.Equals(parts[1], "path", StringComparison.OrdinalIgnoreCase))
                        AddRoot(parts[3], escapedVdfPath: true);
                }
            }

            return roots;
        }

        private static IEnumerable<string> ReadSteamRegistryPaths()
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var locations = new[]
            {
                (RegistryHive.CurrentUser, @"Software\Valve\Steam"),
                (RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam"),
                (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam")
            };

            foreach (var (hive, subKey) in locations)
            {
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using RegistryKey? key = baseKey.OpenSubKey(subKey);
                        if (key?.GetValue("SteamPath") is string steamPath && !string.IsNullOrWhiteSpace(steamPath))
                            paths.Add(steamPath);
                    }
                    catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or System.Security.SecurityException or PlatformNotSupportedException)
                    {
                        // Registry discovery is best-effort; other registry views or the default path may still work.
                    }
                }
            }

            return paths;
        }
    }
}
