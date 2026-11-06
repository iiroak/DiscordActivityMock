using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class ActivityOptionsTests
    {
        private const string Custom = "Custom Activity";

        private static readonly ActivityItem[] Catalog =
        {
            new() { name = "Fortnite", folder = "Win64", exe = "FortniteClient-Win64-Shipping" },
            new() { name = "Fallout 4", folder = "Fallout 4", exe = "Fallout4" },
            new() { name = "Fall Guys", folder = "FallGuys", exe = "FallGuys_client" },
            new()
            {
                name = "EA SPORTS FC 27", folder = "EA SPORTS FC 27", exe = "fc27",
                steamManifest = new SteamManifestItem { appId = "1", name = "FC", stateFlags = "4", installDir = "EA SPORTS FC 27" }
            },
            new() { name = Custom, folder = "Spoof", exe = "spoof" }
        };

        [Fact]
        public void BuildOptionsListsTheCatalogThenTheCustomActivity()
        {
            List<ActivityOption> options = ActivityRules.BuildOptions(Catalog, null, Custom);

            Assert.Equal(new[] { "Fortnite", "Fallout 4", "Fall Guys", "EA SPORTS FC 27 (SteamApps)", Custom },
                         options.Select(option => option.DisplayName));
            Assert.True(options[^1].IsCustom);
            Assert.Equal(1, options.Count(option => option.IsCustom)); // a catalog entry named like the label is skipped
        }

        [Fact]
        public void BuildOptionsWithoutCatalogOnlyHasTheCustomActivity()
        {
            ActivityOption option = Assert.Single(ActivityRules.BuildOptions(null, "anything", Custom));
            Assert.True(option.IsCustom);
        }

        [Fact]
        public void BuildOptionsFiltersByName()
        {
            List<ActivityOption> options = ActivityRules.BuildOptions(Catalog, " fall ", Custom);

            Assert.Equal(new[] { "Fallout 4", "Fall Guys", Custom }, options.Select(option => option.DisplayName));
        }

        [Theory]
        [InlineData("fall guys", "Fall Guys")]   // exact match wins
        [InlineData("Fall", "Fallout 4")]        // otherwise the first prefix match
        [InlineData("out", "Fallout 4")]         // otherwise the first result
        [InlineData("zzz", Custom)]              // nothing matches
        public void ChooseSelectionIndexPrefersTheClosestSearchMatch(string search, string expected)
        {
            List<ActivityOption> options = ActivityRules.BuildOptions(Catalog, search, Custom);

            int index = ActivityRules.ChooseSelectionIndex(options, search, previous: null, resetWhenSearchCleared: false);

            Assert.Equal(expected, options[index].DisplayName);
        }

        [Fact]
        public void ChooseSelectionIndexKeepsThePreviousSelectionAfterAReload()
        {
            List<ActivityOption> before = ActivityRules.BuildOptions(Catalog, null, Custom);
            ActivityOption previous = before[1];
            ActivityItem[] reloaded = Catalog.Select(item => new ActivityItem { name = item.name, folder = item.folder, exe = item.exe }).ToArray();
            List<ActivityOption> after = ActivityRules.BuildOptions(reloaded, null, Custom);

            Assert.Equal(1, ActivityRules.ChooseSelectionIndex(after, null, previous, resetWhenSearchCleared: false));
            Assert.Equal(after.Count - 1, ActivityRules.ChooseSelectionIndex(after, null, previous, resetWhenSearchCleared: true));
            Assert.Equal(after.Count - 1, ActivityRules.ChooseSelectionIndex(after, null, before[^1], resetWhenSearchCleared: false));
        }

        [Theory]
        [InlineData(0, "00:00:00")]
        [InlineData(59, "00:00:59")]
        [InlineData(3661, "01:01:01")]
        [InlineData(90000, "25:00:00")]
        [InlineData(-5, "00:00:00")]
        public void FormatElapsedKeepsCountingHoursPast24(int seconds, string expected)
        {
            Assert.Equal(expected, ActivityRules.FormatElapsed(TimeSpan.FromSeconds(seconds)));
        }

        [Theory]
        [InlineData("Game.exe", "Game")]
        [InlineData(" Game.Shipping ", "Game.Shipping")]
        [InlineData(null, "")]
        public void StripExeExtensionOnlyRemovesExe(string? input, string expected)
        {
            Assert.Equal(expected, ActivityRules.StripExeExtension(input));
        }

        [Fact]
        public async Task ReadUtf8AsyncAcceptsTextUpToTheLimitAndSkipsTheBom()
        {
            byte[] bom = Encoding.UTF8.GetPreamble();
            byte[] body = Encoding.UTF8.GetBytes("[{\"name\":\"Pokémon\"}]");
            byte[] data = bom.Concat(body).ToArray();
            using var stream = new MemoryStream(data);

            string text = await BoundedRead.ReadUtf8Async(stream, data.Length, CancellationToken.None);

            Assert.Equal("[{\"name\":\"Pokémon\"}]", text);
        }

        [Fact]
        public async Task ReadUtf8AsyncRejectsStreamsOverTheLimit()
        {
            using var stream = new MemoryStream(new byte[100_001]);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                BoundedRead.ReadUtf8Async(stream, 100_000, CancellationToken.None));
        }
    }
}
