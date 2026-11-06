using System;
using System.IO;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public sealed class SteamManifestSupportTests : IDisposable
    {
        private const string InstallDir = "EA SPORTS FC 27";
        private const string TargetExe = "fc27";

        private readonly TempDirectory _temp = new();
        private readonly string _sourceFolder;
        private readonly string _sourceExecutable;

        public SteamManifestSupportTests()
        {
            _sourceFolder = _temp.CreateDirectory("source");
            _sourceExecutable = _temp.CreateFile(Path.Combine("source", "wordpad.exe"), PeImageTests.BuildPe(PeImage.MachineI386));
            _temp.CreateFile(Path.Combine("source", "WordpadFilter.dll"), new byte[] { 9, 9, 9 });
            _temp.CreateFile(Path.Combine("source", "en-US", "wordpad.exe.mui"), new byte[] { 7 });
        }

        public void Dispose() => _temp.Dispose();

        private static SteamManifestItem CreateManifest(string name = "EA SPORTS FC™ 27") => new()
        {
            appId = "4080220",
            name = name,
            stateFlags = "4",
            installDir = InstallDir
        };

        private string GameDirectory(string library) => Path.Combine(library, "steamapps", "common", InstallDir);

        private static string ManifestPath(string library) => Path.Combine(library, "steamapps", "appmanifest_4080220.acf");

        [Fact]
        public void PrepareCreatesTheMockAndManifestInAnEmptyLibrary()
        {
            string library = _temp.CreateDirectory("library");
            SteamManifestItem manifest = CreateManifest();

            SteamManifestLaunch launch = SteamManifestSupport.Prepare(manifest, _sourceFolder, _sourceExecutable, TargetExe, new[] { library });

            string gameDirectory = GameDirectory(library);
            Assert.Equal(Path.Combine(gameDirectory, "fc27.exe"), launch.ExecutablePath);
            Assert.Equal(gameDirectory, launch.WorkingDirectory);
            Assert.True(File.Exists(launch.ExecutablePath));
            Assert.True(File.Exists(Path.Combine(gameDirectory, "WordpadFilter.dll")));
            Assert.True(File.Exists(Path.Combine(gameDirectory, "en-US", "fc27.exe.mui")));
            Assert.Equal(SteamManifestSupport.BuildManifest(manifest), File.ReadAllText(launch.ManifestPath));
        }

        [Fact]
        public void PrepareReusesAnIdenticalExistingMock()
        {
            string library = _temp.CreateDirectory("library");

            SteamManifestLaunch first = SteamManifestSupport.Prepare(CreateManifest(), _sourceFolder, _sourceExecutable, TargetExe, new[] { library });
            SteamManifestLaunch second = SteamManifestSupport.Prepare(CreateManifest(), _sourceFolder, _sourceExecutable, TargetExe, new[] { library });

            Assert.Equal(first, second);
        }

        [Fact]
        public void PrepareLeavesADifferentManifestUntouched()
        {
            string library = _temp.CreateDirectory("library");
            SteamManifestItem other = CreateManifest(name: "Another game");
            string manifestPath = ManifestPath(library);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            string otherContent = SteamManifestSupport.BuildManifest(other);
            File.WriteAllText(manifestPath, otherContent);

            Assert.Throws<IOException>(() =>
                SteamManifestSupport.Prepare(CreateManifest(), _sourceFolder, _sourceExecutable, TargetExe, new[] { library }));

            Assert.Equal(otherContent, File.ReadAllText(manifestPath));
            Assert.False(Directory.Exists(GameDirectory(library)));
        }

        [Fact]
        public void PrepareSkipsALibraryWithAnUnrelatedFolder()
        {
            string busyLibrary = _temp.CreateDirectory("busy");
            string freeLibrary = _temp.CreateDirectory("free");
            string unrelatedFile = Path.Combine(GameDirectory(busyLibrary), "save.dat");
            Directory.CreateDirectory(GameDirectory(busyLibrary));
            File.WriteAllText(unrelatedFile, "keep me");

            SteamManifestLaunch launch = SteamManifestSupport.Prepare(
                CreateManifest(), _sourceFolder, _sourceExecutable, TargetExe, new[] { busyLibrary, freeLibrary });

            Assert.StartsWith(freeLibrary, launch.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("keep me", File.ReadAllText(unrelatedFile));
            Assert.False(File.Exists(Path.Combine(GameDirectory(busyLibrary), "fc27.exe")));
            Assert.False(File.Exists(ManifestPath(busyLibrary)));
        }

        [Fact]
        public void PrepareRefusesWhenSeveralLibrariesHaveTheManifest()
        {
            string first = _temp.CreateDirectory("first");
            string second = _temp.CreateDirectory("second");
            foreach (string library in new[] { first, second })
            {
                Directory.CreateDirectory(Path.Combine(library, "steamapps"));
                File.WriteAllText(ManifestPath(library), SteamManifestSupport.BuildManifest(CreateManifest()));
            }

            Assert.Throws<IOException>(() =>
                SteamManifestSupport.Prepare(CreateManifest(), _sourceFolder, _sourceExecutable, TargetExe, new[] { first, second }));
        }

        [Fact]
        public void PrepareFailsWithoutLibraries()
        {
            Assert.Throws<DirectoryNotFoundException>(() =>
                SteamManifestSupport.Prepare(CreateManifest(), _sourceFolder, _sourceExecutable, TargetExe, Array.Empty<string>()));
        }

        [Theory]
        [InlineData("abc", "Name", "4", InstallDir, InstallDir)]
        [InlineData("-1", "Name", "4", InstallDir, InstallDir)]
        [InlineData("10", " ", "4", InstallDir, InstallDir)]
        [InlineData("10", "Name", "x", InstallDir, InstallDir)]
        [InlineData("10", "Name", "4", "a/b", "a/b")]
        [InlineData("10", "Name", "4", "..", "..")]
        [InlineData("10", "Name", "4", "CON", "CON")]
        [InlineData("10", "Name", "4", InstallDir, "Other folder")]
        public void ValidateRejectsInvalidManifests(string appId, string name, string stateFlags, string installDir, string folder)
        {
            var manifest = new SteamManifestItem { appId = appId, name = name, stateFlags = stateFlags, installDir = installDir };
            Assert.Throws<ArgumentException>(() => SteamManifestSupport.Validate(manifest, folder));
        }

        [Fact]
        public void BuildManifestEscapesQuotesAndBackslashes()
        {
            var manifest = new SteamManifestItem { appId = "10", name = "A \"quoted\" \\ name", stateFlags = "4", installDir = "Dir" };

            string content = SteamManifestSupport.BuildManifest(manifest);

            Assert.Contains("\"name\"\t\t\"A \\\"quoted\\\" \\\\ name\"", content);
            Assert.StartsWith("\"AppState\"", content);
        }
    }
}
