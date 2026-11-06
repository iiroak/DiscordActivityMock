using System.Text.Json.Serialization;

namespace DiscordActivityMockV2
{
    public class ActivityItem
    {
        public string name { get; set; } = "";
        public string folder { get; set; } = "";
        public string exe { get; set; } = "";
        public SteamManifestItem? steamManifest { get; set; }
    }

    public class AppSettings
    {
        public bool HideEnabled { get; set; } = true;
        public int HideDelaySeconds { get; set; } = 15;
        public bool AutoStopEnabled { get; set; } = false;
        public int AutoStopMinutes { get; set; } = 30;
        public bool DontShowStartWarning { get; set; } = false;
        public bool IsDarkTheme { get; set; } = true;
    }

    [JsonSerializable(typeof(ActivityItem[]))]
    [JsonSerializable(typeof(SteamManifestItem))]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, WriteIndented = true)]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
