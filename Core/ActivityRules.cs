using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DiscordActivityMockV2
{
    /// <summary>One entry of the activity list: a catalog activity, or the custom activity when <see cref="Activity"/> is null.</summary>
    internal sealed record ActivityOption(ActivityItem? Activity, string DisplayName)
    {
        public bool IsCustom => Activity == null;
    }

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
                throw new ArgumentException(Localization.Get("FolderMustBeRelative"));

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                            .Replace('\\', Path.DirectorySeparatorChar);
            string[] segments = normalized.Split(Path.DirectorySeparatorChar);
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." ||
                                        segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                                        segment.EndsWith(' ') || segment.EndsWith('.') ||
                                        IsReservedDeviceName(segment)))
                throw new ArgumentException(Localization.Get("FolderInvalidSegment"));

            string fullRoot = Path.GetFullPath(root);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, normalized));
            if (!PathGuard.IsWithin(fullRoot, fullPath))
                throw new ArgumentException(Localization.Get("FolderOutsideWorkingDirectory"));
            return fullPath;
        }

        /// <summary>Returns the executable name without ".exe", or throws <see cref="ArgumentException"/>.</summary>
        public static string NormalizeExecutableName(string input)
        {
            string name = StripExeExtension(input);

            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
                Path.IsPathRooted(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.EndsWith(' ') || name.EndsWith('.') || name.Contains('/') || name.Contains('\\'))
                throw new ArgumentException(Localization.Get("ExeNameInvalid"));

            if (IsReservedDeviceName(name))
                throw new ArgumentException(Localization.Get("ExeNameReserved"));

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
                                         ?? throw new JsonException(Localization.Get("CatalogEmpty"));

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
                throw new JsonException(Localization.Get("CatalogNoValidEntries"));
            return uniqueActivities;
        }

        public static string GetDisplayName(ActivityItem activity) =>
            activity.steamManifest == null ? activity.name : $"{activity.name} (SteamApps)";

        /// <summary>
        /// Catalog activities whose name contains <paramref name="searchFilter"/>, followed by the custom activity.
        /// Display names are unique, so the list can be matched by name or by index.
        /// </summary>
        public static List<ActivityOption> BuildOptions(IReadOnlyList<ActivityItem>? activities, string? searchFilter, string customLabel)
        {
            string search = searchFilter?.Trim() ?? "";
            var options = new List<ActivityOption>();
            var displayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { customLabel };
            foreach (ActivityItem activity in activities ?? Array.Empty<ActivityItem>())
            {
                if (search.Length > 0 && !activity.name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;

                string displayName = GetDisplayName(activity);
                if (displayNames.Add(displayName))
                    options.Add(new ActivityOption(activity, displayName));
            }

            options.Add(new ActivityOption(null, customLabel));
            return options;
        }

        /// <summary>
        /// With a search: the exact name match, else the first name that starts with it, else the first result.
        /// Without a search: the previous selection when it is still listed, unless the search was just cleared.
        /// Otherwise the custom activity, which is always last.
        /// </summary>
        public static int ChooseSelectionIndex(
            IReadOnlyList<ActivityOption> options,
            string? searchFilter,
            ActivityOption? previous,
            bool resetWhenSearchCleared)
        {
            int customIndex = options.Count - 1;
            string search = searchFilter?.Trim() ?? "";
            if (search.Length > 0)
            {
                if (customIndex == 0)
                    return customIndex;

                int exact = IndexOf(options, customIndex, option =>
                    string.Equals(option.Activity!.name, search, StringComparison.OrdinalIgnoreCase));
                if (exact >= 0)
                    return exact;

                int prefix = IndexOf(options, customIndex, option =>
                    option.Activity!.name.StartsWith(search, StringComparison.OrdinalIgnoreCase));
                return prefix >= 0 ? prefix : 0;
            }

            if (!resetWhenSearchCleared && previous != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].IsCustom == previous.IsCustom &&
                        string.Equals(options[i].DisplayName, previous.DisplayName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return customIndex;
        }

        /// <summary>Elapsed time as hours:minutes:seconds; hours keep counting past 24.</summary>
        public static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (long)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
        }

        private static int IndexOf(IReadOnlyList<ActivityOption> options, int count, Func<ActivityOption, bool> predicate)
        {
            for (int i = 0; i < count; i++)
            {
                if (predicate(options[i]))
                    return i;
            }
            return -1;
        }
    }
}
