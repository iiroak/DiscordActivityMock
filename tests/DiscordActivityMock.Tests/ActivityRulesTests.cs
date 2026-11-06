using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class ActivityRulesTests
    {
        [Theory]
        [InlineData("Game", "Game")]
        [InlineData("Game.exe", "Game")]
        [InlineData("  Game.EXE ", "Game")]
        [InlineData("Game.Shipping", "Game.Shipping")]
        [InlineData("Risk of Rain 2", "Risk of Rain 2")]
        [InlineData("John Carpenter's Toxic Commando", "John Carpenter's Toxic Commando")]
        public void NormalizeExecutableNameKeepsValidNames(string input, string expected)
        {
            Assert.Equal(expected, ActivityRules.NormalizeExecutableName(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".exe")]
        [InlineData("..")]
        [InlineData("a/b")]
        [InlineData(@"a\b")]
        [InlineData(@"C:\game")]
        [InlineData("name.")]
        [InlineData("na:me")]
        [InlineData("CON")]
        [InlineData("con.exe")]
        [InlineData("COM1.txt")]
        [InlineData("lpt9")]
        public void NormalizeExecutableNameRejectsInvalidNames(string input)
        {
            Assert.Throws<ArgumentException>(() => ActivityRules.NormalizeExecutableName(input));
        }

        [Theory]
        [InlineData("Game", "Game")]
        [InlineData("Game/Binaries/Win64", @"Game\Binaries\Win64")]
        [InlineData(@"Game\Binaries", @"Game\Binaries")]
        [InlineData("Resident Evil Village BIOHAZARD VILLAGE", "Resident Evil Village BIOHAZARD VILLAGE")]
        public void ResolveChildPathStaysInsideTheRoot(string relative, string expectedRelative)
        {
            string root = Path.Combine(Path.GetTempPath(), "DAM-Root");
            Assert.Equal(Path.Combine(root, expectedRelative), ActivityRules.ResolveChildPath(root, relative));
        }

        [Theory]
        [InlineData("")]
        [InlineData("..")]
        [InlineData("../escape")]
        [InlineData("a/../b")]
        [InlineData("a/./b")]
        [InlineData("a//b")]
        [InlineData("a /b")]
        [InlineData("a./b")]
        [InlineData(@"C:\absolute")]
        [InlineData(@"\rooted")]
        [InlineData("CON/b")]
        [InlineData("a/na*me")]
        public void ResolveChildPathRejectsUnsafePaths(string relative)
        {
            string root = Path.Combine(Path.GetTempPath(), "DAM-Root");
            Assert.Throws<ArgumentException>(() => ActivityRules.ResolveChildPath(root, relative));
        }

        [Fact]
        public void ParseActivitiesSkipsInvalidEntriesRemovesDuplicatesAndSorts()
        {
            const string json = """
            [
              { "name": "Zeta", "folder": "Zeta", "exe": "zeta" },
              { "name": "alpha", "folder": "Alpha/Bin", "exe": "alpha.exe" },
              { "name": "ALPHA", "folder": "Other", "exe": "other" },
              { "name": "Escape", "folder": "../outside", "exe": "x" },
              { "name": "NoExe", "folder": "NoExe", "exe": "" },
              { "name": "Reserved", "folder": "Reserved", "exe": "CON" },
              { "name": "BadSteam", "folder": "BadSteam", "exe": "b", "steamManifest": { "appId": "abc", "name": "B", "stateFlags": "4", "installDir": "BadSteam" } },
              { "name": "Steam", "folder": "Steam Game", "exe": "s", "steamManifest": { "appId": "10", "name": "Steam Game", "stateFlags": "4", "installDir": "Steam Game" } },
              null
            ]
            """;

            ActivityItem[] activities = ActivityRules.ParseActivities(json);

            Assert.Equal(new[] { "alpha", "Steam", "Zeta" }, activities.Select(activity => activity.name));
            Assert.Equal("Alpha/Bin", activities[0].folder);
            Assert.NotNull(activities[1].steamManifest);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("""[ { "name": "Bad", "folder": "..", "exe": "x" } ]""")]
        public void ParseActivitiesRejectsCatalogsWithoutValidEntries(string json)
        {
            Assert.ThrowsAny<JsonException>(() => ActivityRules.ParseActivities(json));
        }

        [Fact]
        public void BundledCatalogIsValid()
        {
            string json = File.ReadAllText(Path.Combine(RepositoryPaths.Root, "resources", "ActivityList.json"));

            ActivityItem[] activities = ActivityRules.ParseActivities(json);

            Assert.NotEmpty(activities);
            Assert.Contains(activities, activity => activity.steamManifest != null);
        }

        [Theory]
        [InlineData("CON", true)]
        [InlineData("con.txt", true)]
        [InlineData("COM9", true)]
        [InlineData("CONSOLE", false)]
        [InlineData("COM10", false)]
        [InlineData("Game", false)]
        public void IsReservedDeviceNameMatchesWindowsDeviceNames(string name, bool expected)
        {
            Assert.Equal(expected, ActivityRules.IsReservedDeviceName(name));
        }
    }
}
