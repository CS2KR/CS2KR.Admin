namespace CS2KR.Admin.Models;

public sealed class CachedAdmin
{
    public required long Id { get; init; }
    public required string SteamId { get; init; }
    public required string Name { get; init; }
    public int Immunity { get; init; }
    public int? GroupId { get; init; }
    public string? GroupName { get; init; }
    public required HashSet<string> Flags { get; init; }
    public DateTime? Ends { get; init; }

    public bool HasFlag(string flag)
    {
        if (Flags.Contains("@css/root")) return true;
        return Flags.Contains(flag);
    }
}
