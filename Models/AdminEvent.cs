namespace CS2KR.Admin.Models;

public sealed class AdminEvent
{
    public required long Id { get; init; }
    public required string EventType { get; init; }    // ban/unban/mute/unmute/kick/reload_admins/...
    public string? TargetSteamId { get; init; }
    public long? TargetRecordId { get; init; }
    public string? RawPayload { get; init; }            // raw JSON string
    public int? ServerId { get; init; }
    public DateTime CreatedAt { get; init; }
}
