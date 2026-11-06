using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class UpdateCheckerTests
    {
        private const string ValidDigest = "sha256:697c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b29";
        private const string AssetUrl = "https://github.com/iiroak/DiscordActivityMock/releases/download/v2026.11.0/DiscordActivityMock-v2026.11.0-win-x86-Portable.exe";

        // Shape of https://api.github.com/repos/iiroak/DiscordActivityMock/releases/latest (trimmed).
        private const string ReleaseJson = """
        {
          "tag_name": "v2026.11.0",
          "html_url": "https://github.com/iiroak/DiscordActivityMock/releases/tag/v2026.11.0",
          "assets": [
            {
              "name": "DiscordActivityMock-v2026.11.0-win-x86-Portable.exe",
              "digest": "sha256:697c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b29",
              "browser_download_url": "https://github.com/iiroak/DiscordActivityMock/releases/download/v2026.11.0/DiscordActivityMock-v2026.11.0-win-x86-Portable.exe"
            },
            {
              "name": "DiscordActivityMock-v2026.11.0-win-x86-Portable.zip",
              "digest": "sha256:7de3c6707b053a163cc449284aa1889b8b0b211f4c56a296e5840483b6e88a1c",
              "browser_download_url": "https://github.com/iiroak/DiscordActivityMock/releases/download/v2026.11.0/DiscordActivityMock-v2026.11.0-win-x86-Portable.zip"
            },
            {
              "name": "DiscordActivityMock-v2026.11.0-win-x86-Setup.exe",
              "digest": null,
              "browser_download_url": "https://github.com/iiroak/DiscordActivityMock/releases/download/v2026.11.0/DiscordActivityMock-v2026.11.0-win-x86-Setup.exe"
            }
          ]
        }
        """;

        [Theory]
        [InlineData(ValidDigest)]
        [InlineData("SHA256:697C3A8B4706D1C558C29A3389844627F8C573D0D75AA84EAA1BE545B3F78B29")]
        public void TryParseSha256DigestAcceptsGitHubDigests(string digest)
        {
            Assert.True(UpdateChecker.TryParseSha256Digest(digest, out byte[] hash));
            Assert.Equal(32, hash.Length);
            Assert.Equal(0x69, hash[0]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("697c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b29")]
        [InlineData("sha256:697c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b2")]
        [InlineData("sha256:697c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b290")]
        [InlineData("sha256:g97c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b29")]
        [InlineData("sha512:697c3a8b4706d1c558c29a3389844627f8c573d0d75aa84eaa1be545b3f78b29")]
        public void TryParseSha256DigestRejectsInvalidDigests(string? digest)
        {
            Assert.False(UpdateChecker.TryParseSha256Digest(digest, out _));
        }

        [Theory]
        [InlineData(AssetUrl, true)]
        [InlineData("https://GITHUB.com/iiroak/discordactivitymock/releases/download/v1/file.zip", true)]
        [InlineData("http://github.com/iiroak/DiscordActivityMock/releases/download/v1/file.exe", false)]
        [InlineData("https://github.com/someone/DiscordActivityMock/releases/download/v1/file.exe", false)]
        [InlineData("https://github.com/iiroak/OtherRepo/releases/download/v1/file.exe", false)]
        [InlineData("https://github.com.evil.example/iiroak/DiscordActivityMock/releases/download/v1/file.exe", false)]
        [InlineData("https://user@github.com/iiroak/DiscordActivityMock/releases/download/v1/file.exe", false)]
        [InlineData("https://github.com:8443/iiroak/DiscordActivityMock/releases/download/v1/file.exe", false)]
        [InlineData("https://github.com/iiroak/DiscordActivityMock/releases/download/../../../evil/file.exe", false)]
        [InlineData("https://github.com/iiroak/DiscordActivityMock/releases/tag/v1", false)]
        [InlineData("https://github.com/iiroak/DiscordActivityMock/releases/downloadX/v1/file.exe", false)]
        [InlineData("not a url", false)]
        [InlineData(null, false)]
        public void IsTrustedAssetUrlOnlyAcceptsThisRepositoryReleaseAssets(string? url, bool expected)
        {
            Assert.Equal(expected, UpdateChecker.IsTrustedAssetUrl(url));
        }

        [Theory]
        [InlineData("https://github.com/iiroak/DiscordActivityMock/releases/tag/v2026.11.0", true)]
        [InlineData("https://github.com/iiroak/DiscordActivityMock/releases", true)]
        [InlineData("https://github.com/iiroak/DiscordActivityMock/releasesX", false)]
        [InlineData("https://github.com/iiroak/DiscordActivityMock", false)]
        [InlineData("https://example.com/iiroak/DiscordActivityMock/releases", false)]
        public void IsTrustedReleasePageUrlOnlyAcceptsThisRepositoryReleasePages(string url, bool expected)
        {
            Assert.Equal(expected, UpdateChecker.IsTrustedReleasePageUrl(url));
        }

        [Fact]
        public void ParseReleaseReturnsOnlyVerifiableAssets()
        {
            UpdateInfo? update = UpdateChecker.ParseRelease(ReleaseJson, "2.2.8");

            Assert.NotNull(update);
            Assert.Equal("2026.11.0", update!.Version);
            Assert.Equal("https://github.com/iiroak/DiscordActivityMock/releases/tag/v2026.11.0", update.ReleaseUrl);
            Assert.Equal(AssetUrl, update.PortableExe?.DownloadUrl);
            Assert.NotNull(update.PortableZip);
            Assert.Null(update.Setup); // no digest
            Assert.Same(update.PortableZip, UpdateChecker.GetUpdateAsset(update, UpdatePackageKind.PortableFolder));
            Assert.Null(UpdateChecker.GetUpdateAsset(update, UpdatePackageKind.Installed));
        }

        [Theory]
        [InlineData("2026.11.0")]
        [InlineData("2026.12.0")]
        public void ParseReleaseIgnoresReleasesThatAreNotNewer(string currentVersion)
        {
            Assert.Null(UpdateChecker.ParseRelease(ReleaseJson, currentVersion));
        }

        [Fact]
        public void ParseReleaseFallsBackToTheReleasesPageForUntrustedLinks()
        {
            const string json = """{ "tag_name": "v2026.11.0", "html_url": "https://example.com/phish", "assets": [] }""";

            UpdateInfo? update = UpdateChecker.ParseRelease(json, "2.2.8");

            Assert.Equal(UpdateChecker.ReleasesPageUrl, update?.ReleaseUrl);
        }

        [Fact]
        public void ParseReleaseRejectsResponsesWithoutATag()
        {
            Assert.Throws<JsonException>(() => UpdateChecker.ParseRelease("""{ "assets": [] }""", "2.2.8"));
        }

        [Fact]
        public async Task CopyAndHashAsyncCopiesHashesAndReportsEachPercentOnce()
        {
            byte[] data = new byte[300_000];
            new Random(42).NextBytes(data);
            using var source = new MemoryStream(data);
            using var destination = new MemoryStream();
            var progress = new RecordingProgress();

            byte[] hash = await UpdateChecker.CopyAndHashAsync(source, destination, data.Length, data.Length, progress, CancellationToken.None);

            Assert.Equal(SHA256.HashData(data), hash);
            Assert.Equal(data, destination.ToArray());
            Assert.Equal(100, progress.Values[^1]);
            for (int i = 1; i < progress.Values.Count; i++)
                Assert.True(progress.Values[i] > progress.Values[i - 1]);
        }

        [Fact]
        public async Task CopyAndHashAsyncStopsAtTheSizeLimit()
        {
            using var source = new MemoryStream(new byte[1000]);
            using var destination = new MemoryStream();

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                UpdateChecker.CopyAndHashAsync(source, destination, 999, -1, null, CancellationToken.None));
        }

        [Fact]
        public void GetPortableExecutableNameMatchesTheReleaseAssetName()
        {
            Assert.Equal("DiscordActivityMock-v2026.11.0-win-x86-Portable.exe", UpdateChecker.GetPortableExecutableName("2026.11.0"));
        }

        private sealed class RecordingProgress : IProgress<int>
        {
            public List<int> Values { get; } = new();

            public void Report(int value) => Values.Add(value);
        }
    }
}
