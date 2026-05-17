using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace CS2KR.Admin;

public class DatabaseConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 3306;
    public string Database { get; set; } = "cs2kr";
    public string Username { get; set; } = "root";
    public string Password { get; set; } = "";

    [JsonIgnore]
    public string ConnectionString => $"Server={Host};Port={Port};Database={Database};Uid={Username};Pwd={Password};" +
                                       "Pooling=true;MinimumPoolSize=2;MaximumPoolSize=10;ConnectionLifeTime=60;" +
                                       "DefaultCommandTimeout=10;AllowUserVariables=true;CharSet=utf8mb4";
}

public class DiscordConfig
{
    public string WebhookUrl { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class DurationPreset
{
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("minutes")] public int Minutes { get; set; }
}

public class Config : BasePluginConfig
{
    [JsonPropertyName("Database")]
    public DatabaseConfig Database { get; set; } = new();

    [JsonPropertyName("Discord")]
    public DiscordConfig Discord { get; set; } = new();

    /// <summary>Discord embed 타이틀 클릭 시 연결될 CS2KR 웹 주소 (말미 / 없이).</summary>
    [JsonPropertyName("WebBaseUrl")]
    public string WebBaseUrl { get; set; } = "https://cs2.kr";

    /// <summary>0 = 자동 감지 (ConVar hostport + sa_servers 매칭). 매칭 실패 시 NULL 로 동작.</summary>
    [JsonPropertyName("ServerIdOverride")]
    public int ServerIdOverride { get; set; } = 0;

    [JsonPropertyName("PollIntervalSeconds")]
    public double PollIntervalSeconds { get; set; } = 1.0;

    [JsonPropertyName("BanReasons")]
    public List<string> BanReasons { get; set; } = new() { "핵 사용", "스크립트 사용", "비매너", "광고", "트롤링" };

    [JsonPropertyName("MuteReasons")]
    public List<string> MuteReasons { get; set; } = new() { "욕설", "스팸", "비매너", "광고" };

    [JsonPropertyName("BanDurations")]
    public List<DurationPreset> BanDurations { get; set; } = new()
    {
        new() { Label = "30분", Minutes = 30 },
        new() { Label = "1시간", Minutes = 60 },
        new() { Label = "1일", Minutes = 1440 },
        new() { Label = "7일", Minutes = 10080 },
        new() { Label = "30일", Minutes = 43200 },
        new() { Label = "영구", Minutes = 0 },
    };

    [JsonPropertyName("MuteDurations")]
    public List<DurationPreset> MuteDurations { get; set; } = new()
    {
        new() { Label = "5분", Minutes = 5 },
        new() { Label = "30분", Minutes = 30 },
        new() { Label = "1시간", Minutes = 60 },
        new() { Label = "1일", Minutes = 1440 },
        new() { Label = "영구", Minutes = 0 },
    };

    [JsonPropertyName("BroadcastBans")]
    public bool BroadcastBans { get; set; } = true;

    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = " [CS2KR] ";

    [JsonPropertyName("KickMessageBanned")]
    public string KickMessageBanned { get; set; } = "CS2KR 에서 차단되었습니다. 사유: {0}";
}
