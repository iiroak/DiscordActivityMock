using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiscordActivityMockV2.Tests
{
    public class LocalizationTests
    {
        private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}");
        private static readonly Regex GetCall = new(@"Localization\.Get\(\s*""([^""]+)""");

        [Fact]
        public void EveryKeyHasANonEmptyTextInEveryLanguage()
        {
            foreach (string key in Localization.Keys)
            {
                foreach (Localization.Language language in Enum.GetValues<Localization.Language>())
                {
                    Assert.True(Localization.TryGetExact(key, language, out string text), $"{key} is missing {language}");
                    Assert.False(string.IsNullOrWhiteSpace(text), $"{key} is empty in {language}");
                }
            }
        }

        [Fact]
        public void PlaceholdersMatchAcrossLanguages()
        {
            foreach (string key in Localization.Keys)
            {
                Localization.TryGetExact(key, Localization.Language.English, out string english);
                string[] expected = Placeholders(english);
                foreach (Localization.Language language in Enum.GetValues<Localization.Language>())
                {
                    Localization.TryGetExact(key, language, out string text);
                    Assert.True(expected.SequenceEqual(Placeholders(text)), $"{key} placeholders differ in {language}");
                }
            }
        }

        [Fact]
        public void EveryKeyUsedInTheSourceExists()
        {
            var keys = new HashSet<string>(Localization.Keys);
            var missing = new List<string>();
            foreach (string file in SourceFiles())
            {
                foreach (Match match in GetCall.Matches(File.ReadAllText(file)))
                {
                    string key = match.Groups[1].Value;
                    if (!keys.Contains(key))
                        missing.Add($"{Path.GetFileName(file)}: {key}");
                }
            }

            Assert.Empty(missing);
        }

        [Fact]
        public void EveryKeyIsUsedSomewhere()
        {
            string allSource = string.Join("\n", SourceFiles()
                .Where(file => !string.Equals(Path.GetFileName(file), "Localization.cs", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));
            string[] unused = Localization.Keys.Where(key => !allSource.Contains($"\"{key}\"", StringComparison.Ordinal)).ToArray();

            Assert.Empty(unused);
        }

        [Fact]
        public void GetFallsBackToTheKeyForUnknownText()
        {
            Assert.Equal("NoSuchKey", Localization.Get("NoSuchKey"));
        }

        private static string[] Placeholders(string text) =>
            Placeholder.Matches(text).Select(match => match.Groups[1].Value).Distinct().OrderBy(index => index).ToArray();

        /// <summary>App sources outside build output and the tests themselves.</summary>
        private static IEnumerable<string> SourceFiles()
        {
            string root = RepositoryPaths.Root;
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(file =>
                {
                    string relative = Path.GetRelativePath(root, file);
                    string[] parts = relative.Split(Path.DirectorySeparatorChar);
                    return !parts.Any(part => part is "bin" or "obj" or "tests" or ".git");
                });
        }
    }
}
