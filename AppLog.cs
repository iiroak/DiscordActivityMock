using System;
using System.IO;

namespace DiscordActivityMockV2
{
    internal static class AppLog
    {
        private static readonly object SyncRoot = new();
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DiscordActivityMock", "app.log");

        public static void Error(string operation, Exception exception)
        {
            try
            {
                string? directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string entry = $"{DateTimeOffset.UtcNow:O} ERROR [{operation}] {exception.GetType().Name} (0x{exception.HResult:X8}): {exception.Message}{Environment.NewLine}";
                lock (SyncRoot)
                {
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1024 * 1024)
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
