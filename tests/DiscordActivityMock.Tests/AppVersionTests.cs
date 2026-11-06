using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class AppVersionTests
    {
        [Theory]
        [InlineData("2026.11.0", "2026.11.0")]
        [InlineData("v2026.11.0", "2026.11.0")]
        [InlineData(" V2026.11.1 ", "2026.11.1")]
        [InlineData("2.2.9+abc123", "2.2.9")]
        [InlineData("", "")]
        public void NormalizeRemovesPrefixAndBuildMetadata(string input, string expected)
        {
            Assert.Equal(expected, AppVersion.Normalize(input));
        }

        [Theory]
        [InlineData("2026.11.0", "2.2.8")]      // the switch to calendar versions must update old installs
        [InlineData("2026.12.0", "2026.11.5")]
        [InlineData("2027.1.0", "2026.12.3")]
        [InlineData("2026.11.1", "2026.11.0")]
        [InlineData("3", "2.9.9")]
        [InlineData("2.3", "2.2.9")]
        [InlineData("v2026.11.1", "2026.11.0+sha")]
        public void IsNewerDetectsNewerVersions(string latest, string current)
        {
            Assert.True(AppVersion.IsNewer(latest, current));
        }

        [Theory]
        [InlineData("2026.11.0", "2026.11.0")]
        [InlineData("2026.11.0", "2026.11.0.0")]
        [InlineData("2", "2.0.0")]
        [InlineData("2.2.8", "2026.11.0")]
        [InlineData("2026.10.9", "2026.11.0")]
        [InlineData("not-a-version", "2026.11.0")]
        [InlineData("2026.11.0", "garbage")]
        [InlineData("2026.11.0-beta", "2026.10.0")]
        [InlineData("", "2026.11.0")]
        public void IsNewerRejectsEqualOlderOrInvalidVersions(string latest, string current)
        {
            Assert.False(AppVersion.IsNewer(latest, current));
        }

        [Fact]
        public void ProjectVersionUsesCalendarVersioning()
        {
            XDocument project = XDocument.Load(Path.Combine(RepositoryPaths.Root, "DiscordActivityMockV2.csproj"));
            string? version = null;
            foreach (XElement element in project.Descendants("Version"))
            {
                version = element.Value.Trim();
                break;
            }

            Assert.NotNull(version);
            Assert.Matches(new Regex(@"^\d{4}\.(?:[1-9]|1[0-2])\.\d+$"), version);
        }
    }
}
