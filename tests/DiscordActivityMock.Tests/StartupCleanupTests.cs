using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public sealed class StartupCleanupTests : IDisposable
    {
        private const string Id1 = "0123456789abcdef0123456789abcdef";
        private const string Id2 = "fedcba9876543210fedcba9876543210";
        private const string Id3 = "00000000000000000000000000000000";
        private const string Id4 = "11111111111111111111111111111111";

        private readonly TempDirectory _temp = new();

        public void Dispose() => _temp.Dispose();

        private string CreateSession(bool withLockFile)
        {
            string session = _temp.CreateDirectory("Sessions", Guid.NewGuid().ToString("N"));
            File.WriteAllText(Path.Combine(session, "data.txt"), "x");
            if (withLockFile)
                File.WriteAllText(Path.Combine(session, StartupCleanup.SessionLockFileName), "");
            return session;
        }

        [Fact]
        public void CleanOrphanedSessionsKeepsLockedAndRecentSessions()
        {
            string sessionsRoot = _temp.Combine("Sessions");
            string locked = CreateSession(withLockFile: false);
            string crashed = CreateSession(withLockFile: true);
            string recentLegacy = CreateSession(withLockFile: false);
            string oldLegacy = CreateSession(withLockFile: false);
            string notOurs = _temp.CreateDirectory("Sessions", "keep-me");
            Directory.SetLastWriteTimeUtc(oldLegacy, DateTime.UtcNow.AddDays(-2));
            Directory.SetLastWriteTimeUtc(notOurs, DateTime.UtcNow.AddDays(-2));

            using (StartupCleanup.AcquireSessionLock(locked))
            {
                StartupCleanup.CleanOrphanedSessions(sessionsRoot, DateTime.UtcNow);

                Assert.True(Directory.Exists(locked));
            }

            Assert.False(Directory.Exists(crashed));
            Assert.True(Directory.Exists(recentLegacy));
            Assert.False(Directory.Exists(oldLegacy));
            Assert.True(Directory.Exists(notOurs));
        }

        [Fact]
        public void CleanDownloadedUpdatesOnlyRemovesOldDownloadFolders()
        {
            string oldDownload = _temp.CreateDirectory(UpdateChecker.TemporaryDownloadPrefix + Id1);
            string recentDownload = _temp.CreateDirectory(UpdateChecker.TemporaryDownloadPrefix + Id2);
            string unrelated = _temp.CreateDirectory("SomethingElse");
            Directory.SetLastWriteTimeUtc(oldDownload, DateTime.UtcNow.AddHours(-2));
            Directory.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddHours(-2));

            StartupCleanup.CleanDownloadedUpdates(_temp.FullPath, DateTime.UtcNow);

            Assert.False(Directory.Exists(oldDownload));
            Assert.True(Directory.Exists(recentDownload));
            Assert.True(Directory.Exists(unrelated));
        }

        [Fact]
        public void FindObsoleteUpdateFoldersKeepsCurrentNewerAndRunningFolders()
        {
            string updates = _temp.CreateDirectory("updates");
            string older = _temp.CreateDirectory("updates", "2.2.8-" + Id1);
            string same = _temp.CreateDirectory("updates", "2026.11.0-" + Id2);
            string newer = _temp.CreateDirectory("updates", "2026.12.0-" + Id3);
            string olderParentOfCurrent = _temp.CreateDirectory("updates", "2.2.9-" + Id4);
            string currentExecutableDirectory = _temp.CreateDirectory("updates", "2.2.9-" + Id4, "updates", "2026.11.0-" + Id1);
            _temp.CreateDirectory("updates", "notes");

            var obsolete = StartupCleanup.FindObsoleteUpdateFolders(updates, "2026.11.0", currentExecutableDirectory);

            Assert.Equal(new[] { older }, obsolete);
            Assert.DoesNotContain(same, obsolete);
            Assert.DoesNotContain(newer, obsolete);
            Assert.DoesNotContain(olderParentOfCurrent, obsolete);
        }

        [Fact]
        public void FindObsoleteUpdateFoldersHandlesMissingFolders()
        {
            Assert.Empty(StartupCleanup.FindObsoleteUpdateFolders(_temp.Combine("missing"), "2026.11.0", _temp.FullPath));
            Assert.Empty(StartupCleanup.FindObsoleteUpdateFolders(null, "2026.11.0", _temp.FullPath));
        }

        [Fact]
        public void CleanOldExtractionsOnlyRemovesOlderVersionsOfThisApp()
        {
            string extractionBase = _temp.CreateDirectory(".net");
            string older = _temp.CreateDirectory(".net", "DiscordActivityMock-v2.2.8-win-x86-Portable", "abc");
            string launcher = _temp.CreateDirectory(".net", "DiscordActivityMock-v2.2.4-win-x86-Portable");
            string same = _temp.CreateDirectory(".net", "DiscordActivityMock-v2026.11.0-win-x86-Portable");
            string newer = _temp.CreateDirectory(".net", "DiscordActivityMock-v2026.12.0-win-x86-Portable");
            string pullRequest = _temp.CreateDirectory(".net", "DiscordActivityMock-vpr-12-win-x86-Portable");
            string otherApp = _temp.CreateDirectory(".net", "OtherApp");
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DiscordActivityMock-v2.2.4-win-x86-Portable" };

            StartupCleanup.CleanOldExtractions(new[] { extractionBase }, "2026.11.0", keep);

            Assert.False(Directory.Exists(Path.GetDirectoryName(older)));
            Assert.True(Directory.Exists(launcher)); // the user's original executable still extracts itself here
            Assert.True(Directory.Exists(same));
            Assert.True(Directory.Exists(newer));
            Assert.True(Directory.Exists(pullRequest));
            Assert.True(Directory.Exists(otherApp));
        }

        [Fact]
        public void GetLauncherNamesListsThePortableExecutablesInTheRootFolder()
        {
            string root = _temp.CreateDirectory("root");
            _temp.CreateFile(Path.Combine("root", "DiscordActivityMock-v2.2.4-win-x86-Portable.exe"));
            _temp.CreateFile(Path.Combine("root", "notes.txt"));

            Assert.Equal(new[] { "DiscordActivityMock-v2.2.4-win-x86-Portable" }, StartupCleanup.GetLauncherNames(root));
            Assert.Empty(StartupCleanup.GetLauncherNames(null));
        }

        [Fact]
        public void GetUpdatesDirectoriesIncludesFoldersNestedByOlderVersions()
        {
            const string root = @"C:\Games\DAM";
            const string localAppData = @"C:\Users\me\AppData\Local";
            string processPath = root + @"\updates\2.2.5-" + Id1 + @"\updates\2026.11.0-" + Id2 + @"\app.exe";
            PortableRoot portableRoot = PortableLayout.Resolve(processPath, localAppData);

            List<string> directories = StartupCleanup.GetUpdatesDirectories(processPath, portableRoot);

            Assert.Contains(root + @"\updates", directories);
            Assert.Contains(root + @"\updates\2.2.5-" + Id1 + @"\updates", directories);
            Assert.Contains(portableRoot.FallbackUpdatesDirectory, directories);
            Assert.DoesNotContain(directories, directory => !directory.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                                                            !directory.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("DiscordActivityMock-v2026.11.0-win-x86-Portable", "2026.11.0")]
        [InlineData("DiscordActivityMock-v2.2.8-win-x86-Portable", "2.2.8")]
        public void TryParseExtractionFolderNameReadsTheVersion(string name, string expected)
        {
            Assert.True(StartupCleanup.TryParseExtractionFolderName(name, out string version));
            Assert.Equal(expected, version);
        }

        [Theory]
        [InlineData("DiscordActivityMockV2")]
        [InlineData("DiscordActivityMock-v2.2.8-win-x86-Portable.exe")]
        [InlineData(null)]
        public void TryParseExtractionFolderNameRejectsOtherNames(string? name)
        {
            Assert.False(StartupCleanup.TryParseExtractionFolderName(name, out _));
        }
    }
}
