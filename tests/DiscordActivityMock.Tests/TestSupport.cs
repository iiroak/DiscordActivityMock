using System;
using System.IO;

namespace DiscordActivityMockV2.Tests
{
    /// <summary>A unique folder under %TEMP% that is deleted when the test finishes.</summary>
    internal sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            FullPath = Path.Combine(Path.GetTempPath(), "DAM-Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(FullPath);
        }

        public string FullPath { get; }

        public string Combine(params string[] parts)
        {
            string path = FullPath;
            foreach (string part in parts)
                path = Path.Combine(path, part);
            return path;
        }

        public string CreateDirectory(params string[] parts)
        {
            string path = Combine(parts);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath, byte[]? content = null)
        {
            string path = Combine(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content ?? new byte[] { 1, 2, 3 });
            return path;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(FullPath))
                    Directory.Delete(FullPath, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static class RepositoryPaths
    {
        private static readonly Lazy<string> _root = new(FindRoot);

        /// <summary>The folder that contains DiscordActivityMockV2.csproj.</summary>
        public static string Root => _root.Value;

        private static string FindRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "DiscordActivityMockV2.csproj")))
                    return directory.FullName;
            }

            throw new InvalidOperationException("Could not find the repository root above " + AppContext.BaseDirectory);
        }
    }
}
