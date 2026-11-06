using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class PathGuardTests
    {
        [Theory]
        [InlineData(@"C:\root", @"C:\root\child")]
        [InlineData(@"C:\root\", @"C:\ROOT\child\file.txt")]
        [InlineData(@"C:\", @"C:\anything")]
        public void IsWithinAcceptsChildren(string root, string path)
        {
            Assert.True(PathGuard.IsWithin(root, path));
        }

        [Theory]
        [InlineData(@"C:\root", @"C:\root")]
        [InlineData(@"C:\root", @"C:\rootsibling\file.txt")]
        [InlineData(@"C:\root", @"C:\root\..\other")]
        [InlineData(@"C:\root", @"D:\root\child")]
        [InlineData(@"C:\root", "")]
        [InlineData("", @"C:\root\child")]
        [InlineData(@"C:\root", null)]
        public void IsWithinRejectsEverythingElse(string? root, string? path)
        {
            Assert.False(PathGuard.IsWithin(root, path));
        }

        [Theory]
        [InlineData(@"C:\root", @"c:\ROOT\")]
        [InlineData(@"C:\root\child\..", @"C:\root")]
        public void IsSamePathIgnoresCaseAndTrailingSeparators(string left, string right)
        {
            Assert.True(PathGuard.IsSamePath(left, right));
        }

        [Theory]
        [InlineData(@"C:\root", @"C:\root\child")]
        [InlineData(@"C:\root", null)]
        [InlineData(null, null)]
        public void IsSamePathRejectsDifferentPaths(string? left, string? right)
        {
            Assert.False(PathGuard.IsSamePath(left, right));
        }
    }
}
