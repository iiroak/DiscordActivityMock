using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class SmokeTests
    {
        [Fact]
        public void CoreSourcesAreLinkedIntoTheTestAssembly()
        {
            string manifest = SteamManifestSupport.BuildManifest(new SteamManifestItem
            {
                appId = "10",
                name = "Test",
                stateFlags = "4",
                installDir = "Test"
            });

            Assert.Contains("\"appid\"\t\t\"10\"", manifest);
            Assert.False(string.IsNullOrWhiteSpace(Localization.Get("Start")));
        }
    }
}
