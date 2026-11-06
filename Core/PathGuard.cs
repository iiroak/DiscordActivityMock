using System;
using System.IO;
using System.Security;

namespace DiscordActivityMockV2
{
    /// <summary>Path comparisons used before the app creates, moves, or deletes files.</summary>
    internal static class PathGuard
    {
        /// <summary>True when <paramref name="path"/> is strictly inside <paramref name="root"/>.</summary>
        public static bool IsWithin(string? root, string? path)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string fullRoot = WithTrailingSeparator(Path.GetFullPath(root));
                string fullPath = Path.GetFullPath(path);
                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                return false;
            }
        }

        /// <summary>True when both paths name the same location, ignoring case and trailing separators.</summary>
        public static bool IsSamePath(string? left, string? right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            try
            {
                return string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (IsPathException(ex))
            {
                return false;
            }
        }

        private static string WithTrailingSeparator(string path) =>
            Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

        private static bool IsPathException(Exception ex) =>
            ex is ArgumentException or IOException or NotSupportedException or SecurityException;
    }
}
