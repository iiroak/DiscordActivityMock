using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DiscordActivityMockV2
{
    /// <summary>Validation of activity folder and executable names, and parsing of the activity catalog.</summary>
    internal static class ActivityRules
    {
        private static readonly string[] ReservedDeviceNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// Resolves a relative folder such as "Game/Binaries/Win64" inside <paramref name="root"/>.
        /// Throws <see cref="ArgumentException"/> for absolute paths, "..", empty or invalid segments.
        /// </summary>
        public static string ResolveChildPath(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new ArgumentException("The folder must be a relative path.");

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                            .Replace('\\', Path.DirectorySeparatorChar);
            string[] segments = normalized.Split(Path.DirectorySeparatorChar);
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." ||
                                        segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                                        segment.EndsWith(' ') || segment.EndsWith('.') ||
                                        IsReservedDeviceName(segment)))
                throw new ArgumentException("The folder name contains an invalid path segment.");

            string fullRoot = Path.GetFullPath(root);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalized));
            if (!PathGuard.IsWithin(fullRoot, fullPath))
                throw new ArgumentException("The folder must remain inside the private working directory.");
            return fullPath;
        }

        /// <summary>Returns the executable name without ".exe", or throws <see cref="ArgumentException"/>.</summary>
        public static string NormalizeExecutableName(string input)
        {
            string name = StripExeExtension(input);

            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
                Path.IsPathRooted(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.EndsWith(' ') || name.EndsWith('.') || name.Contains('/') || name.Contains('\\'))
                throw new ArgumentException("The executable name is invalid.");

            if (IsReservedDeviceName(name))
                throw new ArgumentException("The executable name is reserved by Windows.");

            return name;
        }

        /// <summary>Trims the input and removes a trailing ".exe"; other dots are part of the name.</summary>
        public static string StripExeExtension(string? input)
        {
            string name = (input ?? "").Trim();
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        }

        /// <summary>Windows reserves these names even with an extension, for example "COM1.txt".</summary>
        public static bool IsReservedDeviceName(string name)
        {
            string deviceName = name.Split('.')[0].TrimEnd();
            return ReservedDeviceNames.Contains(deviceName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses the catalog JSON, skips malformed entries, removes duplicate names, and sorts by name.
        /// Throws <see cref="JsonException"/> when no valid entry remains.
        /// </summary>
        public static ActivityItem[] ParseActivities(string json)
        {
            ActivityItem?[] activities = JsonSerializer.Deserialize(json, AppJsonContext.Default.ActivityItemArray)
                                         ?? throw new JsonException("The activity catalog is empty.");

            var validActivities = new List<ActivityItem>();
            foreach (ActivityItem? activity in activities)
            {
                if (activity == null || string.IsNullOrWhiteSpace(activity.name) ||
                    string.IsNullOrWhiteSpace(activity.folder) || string.IsNullOrWhiteSpace(activity.exe))
                    continue;

                try
                {
                    activity.name = activity.name.Trim();
                    activity.folder = activity.folder.Trim();
                    activity.exe = activity.exe.Trim();
                    ResolveChildPath(Path.GetTempPath(), activity.folder);
                    NormalizeExecutableName(activity.exe);
                    if (activity.steamManifest != null)
                        SteamManifestSupport.Validate(activity.steamManifest, activity.folder);
                    validActivities.Add(activity);
                }
                catch (ArgumentException)
                {
                    // Ignore malformed entries from the remote catalog.
                }
            }

            ActivityItem[] uniqueActivities = validActivities
                .GroupBy(activity => activity.name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(activity => activity.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (uniqueActivities.Length == 0)
                throw new JsonException("The activity catalog did not contain any valid entries.");
            return uniqueActivities;
        }
    }
}
