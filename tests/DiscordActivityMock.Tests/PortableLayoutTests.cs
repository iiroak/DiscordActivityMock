using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class PortableLayoutTests
    {
        private const string Id1 = "0123456789abcdef0123456789abcdef";
        private const string Id2 = "fedcba9876543210fedcba9876543210";
        private const string Root = @"C:\Games\DAM";
        private const string LocalAppData = @"C:\Users\me\AppData\Local";
        private const string Exe = "DiscordActivityMock-v2026.11.0-win-x86-Portable.exe";

        [Theory]
        [InlineData("2026.11.0-" + Id1, "2026.11.0")]
        [InlineData("2.2.8-" + Id2, "2.2.8")]
        [InlineData("2026.11.0-beta-" + Id1, "2026.11.0-beta")]
        public void TryParseUpdateFolderNameReadsTheVersion(string folderName, string expectedVersion)
        {
            Assert.True(PortableLayout.TryParseUpdateFolderName(folderName, out string version));
            Assert.Equal(expectedVersion, version);
        }

        [Theory]
        [InlineData("updates")]
        [InlineData("2026.11.0")]
        [InlineData("2026.11.0-0123456789abcdef0123456789abcde")]
        [InlineData("-" + Id1)]
        [InlineData("2026 11-" + Id1)]
        [InlineData(null)]
        public void TryParseUpdateFolderNameRejectsOtherFolders(string? folderName)
        {
            Assert.False(PortableLayout.TryParseUpdateFolderName(folderName, out _));
        }

        [Fact]
        public void CreateUpdateFolderNameCanBeParsedBack()
        {
            Assert.True(PortableLayout.TryParseUpdateFolderName(PortableLayout.CreateUpdateFolderName("2026.11.0"), out string version));
            Assert.Equal("2026.11.0", version);
        }

        [Theory]
        [InlineData(Root + @"\" + Exe, Root)]
        [InlineData(Root + @"\updates\2026.11.0-" + Id1 + @"\" + Exe, Root)]
        [InlineData(Root + @"\UPDATES\2026.11.0-" + Id1 + @"\" + Exe, Root)]
        [InlineData(Root + @"\updates\2.2.9-" + Id1 + @"\updates\2026.11.0-" + Id2 + @"\" + Exe, Root)]
        [InlineData(Root + @"\other\2026.11.0-" + Id1 + @"\" + Exe, Root + @"\other\2026.11.0-" + Id1)]
        [InlineData(Root + @"\updates\my-copy\" + Exe, Root + @"\updates\my-copy")]
        public void GetRootDirectoryCollapsesUpdateFolders(string processPath, string expectedRoot)
        {
            Assert.Equal(expectedRoot, PortableLayout.GetRootDirectory(processPath));
        }

        [Fact]
        public void ResolveUsesTheSameKeyForTheOriginalExecutableAndItsUpdates()
        {
            PortableRoot original = PortableLayout.Resolve(Root + @"\" + Exe, LocalAppData);
            PortableRoot updated = PortableLayout.Resolve(Root + @"\updates\2026.12.0-" + Id1 + @"\" + Exe, LocalAppData);

            Assert.Equal(original, updated);
            Assert.Equal(Root + @"\updates", original.PreferredUpdatesDirectory);
            Assert.Equal(Path.Combine(LocalAppData, "DiscordActivityMock", "PortableUpdates", original.Key), original.FallbackUpdatesDirectory);
        }

        [Fact]
        public void ResolveKeepsTheKeyWhenRunningFromTheLocalAppDataFallback()
        {
            const string key = "ABCDEF0123456789";
            string keyDirectory = Path.Combine(LocalAppData, "DiscordActivityMock", "PortableUpdates", key);
            string processPath = Path.Combine(keyDirectory, "2026.11.0-" + Id1, Exe);

            PortableRoot root = PortableLayout.Resolve(processPath, LocalAppData);

            Assert.Equal(key, root.Key);
            Assert.Null(root.PreferredUpdatesDirectory);
            Assert.Equal(keyDirectory, root.FallbackUpdatesDirectory);
        }

        [Fact]
        public void GetDirectoryKeyMatchesTheFormatOfVersion228()
        {
            string expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Root.ToUpperInvariant())))[..16];

            Assert.Equal(expected, PortableLayout.GetDirectoryKey(Root));
            Assert.True(PortableLayout.IsDirectoryKey(expected));
        }

        [Theory]
        [InlineData("2026.11.0", @"C:\a\app.exe", @"C:\A\APP.EXE", "2026.12.0", true, "Keep")]
        [InlineData("2026.11.0", @"C:\a\app.exe", @"C:\a\updates\x\app.exe", "2026.12.0", true, "Redirect")]
        [InlineData("2.2.8", @"C:\a\app.exe", @"C:\a\updates\x\app.exe", "2026.11.0", true, "Redirect")]
        [InlineData("2026.12.0", @"C:\a\app.exe", @"C:\a\updates\x\app.exe", "2026.11.0", true, "DeleteMarker")]
        [InlineData("2026.11.0", @"C:\a\app.exe", @"C:\a\updates\x\app.exe", "2026.11.0", true, "DeleteMarker")]
        [InlineData("2026.11.0", @"C:\a\app.exe", @"C:\a\updates\x\app.exe", null, true, "DeleteMarker")]
        [InlineData("2026.11.0", @"C:\a\app.exe", @"C:\a\updates\x\app.exe", "2026.12.0", false, "DeleteMarker")]
        public void DecideRedirectOnlyOpensExistingNewerCopies(
            string currentVersion, string currentPath, string targetPath, string? targetVersion, bool targetExists, string expected)
        {
            // RedirectDecision is internal, so the expected value is passed by name.
            Assert.Equal(Enum.Parse<RedirectDecision>(expected),
                         PortableLayout.DecideRedirect(currentVersion, currentPath, targetPath, targetVersion, targetExists));
        }
    }
}
