using System;
using System.IO;

namespace DiscordActivityMockV2
{
    /// <summary>Local diagnostic log at %LOCALAPPDATA%\DiscordActivityMock\app.log, rotated after 1 MiB.</summary>
    internal static class AppLog
    {
        private const long MaxLogBytes = 1024 * 1024;
        private static readonly object SyncRoot = new();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscordActivityMock", "app.log");

        /// <summary>Writes the full exception, including inner exceptions and stack traces.</summary>
        public static void Error(string operation, Exception exception) =>
            Write("ERROR", operation, $"(0x{exception.HResult:X8}) {exception}");

        public static void Warning(string operation, string message) => Write("WARN", operation, message);

        private static void Write(string level, string operation, string message)
        {
            try
            {
                string? directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string entry = $"{DateTimeOffset.UtcNow:O} {level} [{operation}] {message}{Environment.NewLine}";
                lock (SyncRoot)
                {
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                        File.Move(LogPath, LogPath + ".old", true);
                    File.AppendAllText(LogPath, entry);
                }
            }
            catch
            {
                // Logging must never prevent the application from recovering.
            }
        }
    }
}
