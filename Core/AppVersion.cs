using System;
using System.Reflection;

namespace DiscordActivityMockV2
{
    /// <summary>
    /// The app version comes from &lt;Version&gt; in DiscordActivityMockV2.csproj.
    /// Releases use calendar versioning: YEAR.MONTH.N, for example 2026.11.0.
    /// </summary>
    internal static class AppVersion
    {
        private static readonly Lazy<string> _current = new(ReadCurrent);

        public static string Current => _current.Value;

        /// <summary>Removes a leading "v" and any "+build" metadata.</summary>
        public static string Normalize(string? value)
        {
            string normalized = (value ?? "").Trim().TrimStart('v', 'V');
            int metadataStart = normalized.IndexOf('+');
            return metadataStart >= 0 ? normalized[..metadataStart] : normalized;
        }

        /// <summary>Parses 1 to 4 numeric parts; missing parts count as 0.</summary>
        public static bool TryParse(string? value, out Version version)
        {
            version = new Version(0, 0);
            string normalized = Normalize(value);
            if (normalized.Length == 0)
                return false;

            string padded = normalized.Split('.').Length switch
            {
                1 => normalized + ".0.0",
                2 => normalized + ".0",
                _ => normalized
            };

            if (!Version.TryParse(padded, out Version? parsed))
                return false;

            // Compare with three parts so 2026.11.0 equals 2026.11.0.0.
            version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
            return true;
        }

        public static bool IsNewer(string? latest, string? current) =>
            TryParse(latest, out Version latestVersion) &&
            TryParse(current, out Version currentVersion) &&
            latestVersion > currentVersion;

        private static string ReadCurrent()
        {
            Assembly assembly = typeof(AppVersion).Assembly;
            string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (TryParse(informational, out _))
                return Normalize(informational);

            Version? assemblyVersion = assembly.GetName().Version;
            return assemblyVersion == null
                ? "0.0.0"
                : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(0, assemblyVersion.Build)}";
        }
    }
}
