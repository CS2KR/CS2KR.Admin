namespace CS2KR.Admin.Models;

public sealed class CachedBan
{
    public required long Id { get; init; }
    public required string PlayerSteamId { get; init; }
    public string? PlayerName { get; init; }
    public string? PlayerIp { get; init; }
    public required string Reason { get; init; }
    public int DurationMinutes { get; init; }
    public DateTime? Ends { get; init; }
    public required string Status { get; init; }
    public int? ServerId { get; init; }
}

public sealed class CachedMute
{
    public required long Id { get; init; }
    public required string PlayerSteamId { get; init; }
    public string? PlayerName { get; init; }
    public required string Reason { get; init; }
    public required string Type { get; init; }  // GAG | MUTE | SILENCE
    public int DurationMinutes { get; init; }
    public DateTime? Ends { get; init; }
    public required string Status { get; init; }
    public int? ServerId { get; init; }
}
