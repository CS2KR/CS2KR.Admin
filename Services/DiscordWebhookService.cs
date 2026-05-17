using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

/// <summary>
/// CS2KR Discord webhook — action 별 리치 embed 발신.
/// 디자인 원칙:
///   - 단일 publisher (EventRepository.TryClaimDiscordAsync 로 dedup)
///   - 사유는 description (4096자) 에 — field value (1024) 한도에 걸리지 않게
///   - SteamID 는 클릭 가능한 프로필 링크
///   - action 별 emoji + 의미 있는 색상
///   - 한글 라벨, footer 에 cs2.kr 브랜드
/// </summary>
public sealed class DiscordWebhookService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly DiscordConfig _config;
    private readonly string _webBaseUrl;
    private readonly ILogger _logger;

    private const string BRAND_NAME = "CS2.KR";
    private static readonly TimeSpan KST_OFFSET = TimeSpan.FromHours(9);

    public DiscordWebhookService(DiscordConfig config, string webBaseUrl, ILogger logger)
    {
        _config = config;
        _webBaseUrl = webBaseUrl.TrimEnd('/');
        _logger = logger;
    }

    /// <summary>해당 플레이어의 공개 제재 이력 페이지 (누구나 열람 가능).</summary>
    private string PublicBansUrl(string steamId64) => $"{_webBaseUrl}/bans?q={steamId64}";

    // ───────────────────────── 공개 API ─────────────────────────

    public void SendBan(BanInfo info) => Send(BuildBanEmbed(info));
    public void SendUnban(UnbanInfo info) => Send(BuildUnbanEmbed(info));
    public void SendBanEdit(BanEditInfo info) => Send(BuildBanEditEmbed(info));
    public void SendMute(MuteInfo info) => Send(BuildMuteEmbed(info));
    public void SendUnmute(UnmuteInfo info) => Send(BuildUnmuteEmbed(info));
    public void SendKick(KickInfo info) => Send(BuildKickEmbed(info));
    public void SendRcon(RconInfo info) => Send(BuildRconEmbed(info));
    public void SendMapChange(MapInfo info) => Send(BuildMapEmbed(info));

    // ───────────────────────── 정보 구조 ─────────────────────────

    public sealed record BanInfo(
        long? RecordId,
        string TargetSteamId, string? TargetName, string? TargetAvatar,
        string AdminSteamId, string AdminName,
        string Reason, int DurationMinutes);

    public sealed record UnbanInfo(
        long? RecordId,
        string TargetSteamId, string? TargetName, string? TargetAvatar,
        string AdminSteamId, string AdminName,
        string Reason);

    public sealed record BanEditInfo(
        long? RecordId,
        string TargetSteamId, string? TargetName, string? TargetAvatar,
        string AdminSteamId, string AdminName,
        string Reason, int DurationMinutes);

    public sealed record MuteInfo(
        long? RecordId,
        string TargetSteamId, string? TargetName, string? TargetAvatar,
        string AdminSteamId, string AdminName,
        string Reason, int DurationMinutes,
        string Type);

    public sealed record UnmuteInfo(
        long? RecordId,
        string TargetSteamId, string? TargetName, string? TargetAvatar,
        string AdminSteamId, string AdminName,
        string Reason, string? Type);

    public sealed record KickInfo(
        string TargetSteamId, string? TargetName, string? TargetAvatar,
        string AdminSteamId, string AdminName,
        string Reason);

    public sealed record RconInfo(
        string AdminSteamId, string AdminName,
        string Command);

    public sealed record MapInfo(
        string AdminSteamId, string AdminName,
        string Map);

    // ───────────────────────── 빌더 ─────────────────────────

    private object BuildBanEmbed(BanInfo i) => Embed(
        title: "🔨 CS2.KR 밴 등록",
        color: 0xE74C3C,
        description: i.Reason,
        url: PublicBansUrl(i.TargetSteamId),
        thumbnailUrl: i.TargetAvatar,
        fields: new[]
        {
            Field("대상", PlayerLink(i.TargetName, i.TargetSteamId), true),
            Field("발급자", AdminLink(i.AdminName, i.AdminSteamId), true),
            Field("기간", DurationText(i.DurationMinutes), false),
        });

    private object BuildUnbanEmbed(UnbanInfo i) => Embed(
        title: "✅ CS2.KR 밴 해제",
        color: 0x2ECC71,
        description: i.Reason,
        url: PublicBansUrl(i.TargetSteamId),
        thumbnailUrl: i.TargetAvatar,
        fields: new[]
        {
            Field("대상", PlayerLink(i.TargetName, i.TargetSteamId), true),
            Field("해제자", AdminLink(i.AdminName, i.AdminSteamId), true),
        });

    private object BuildBanEditEmbed(BanEditInfo i) => Embed(
        title: "✏️ CS2.KR 밴 수정",
        color: 0xF39C12,
        description: i.Reason,
        url: PublicBansUrl(i.TargetSteamId),
        thumbnailUrl: i.TargetAvatar,
        fields: new[]
        {
            Field("대상", PlayerLink(i.TargetName, i.TargetSteamId), true),
            Field("수정자", AdminLink(i.AdminName, i.AdminSteamId), true),
            Field("기간", DurationText(i.DurationMinutes), false),
        });

    private object BuildMuteEmbed(MuteInfo i)
    {
        var (emoji, label, color) = i.Type switch
        {
            "GAG"     => ("💬", "채팅 차단",      0xE67E22),
            "MUTE"    => ("🎙️", "음성 차단",      0xE67E22),
            "SILENCE" => ("🔇", "전체 차단",      0xC0392B),
            _         => ("🔇", i.Type,           0xE67E22),
        };
        return Embed(
            title: $"{emoji} CS2.KR {label}",
            color: color,
            description: i.Reason,
            url: PublicBansUrl(i.TargetSteamId),
            thumbnailUrl: i.TargetAvatar,
            fields: new[]
            {
                Field("대상", PlayerLink(i.TargetName, i.TargetSteamId), true),
                Field("발급자", AdminLink(i.AdminName, i.AdminSteamId), true),
                Field("기간", DurationText(i.DurationMinutes), false),
            });
    }

    private object BuildUnmuteEmbed(UnmuteInfo i)
    {
        var label = i.Type switch
        {
            "GAG"     => "채팅 차단 해제",
            "MUTE"    => "음성 차단 해제",
            "SILENCE" => "전체 차단 해제",
            _         => "차단 해제",
        };
        return Embed(
            title: $"✅ CS2.KR {label}",
            color: 0x2ECC71,
            description: i.Reason,
            url: PublicBansUrl(i.TargetSteamId),
            thumbnailUrl: i.TargetAvatar,
            fields: new[]
            {
                Field("대상", PlayerLink(i.TargetName, i.TargetSteamId), true),
                Field("해제자", AdminLink(i.AdminName, i.AdminSteamId), true),
            });
    }

    private object BuildKickEmbed(KickInfo i) => Embed(
        title: "👢 CS2.KR 킥",
        color: 0xE67E22,
        description: i.Reason,
        url: PublicBansUrl(i.TargetSteamId),
        thumbnailUrl: i.TargetAvatar,
        fields: new[]
        {
            Field("대상", PlayerLink(i.TargetName, i.TargetSteamId), true),
            Field("발급자", AdminLink(i.AdminName, i.AdminSteamId), true),
        });

    private object BuildRconEmbed(RconInfo i) => Embed(
        title: "💻 CS2.KR RCON",
        color: 0x95A5A6,
        description: $"```\n{Trim(i.Command, 3900)}\n```",
        fields: new[]
        {
            Field("실행자", AdminLink(i.AdminName, i.AdminSteamId), true),
        });

    private object BuildMapEmbed(MapInfo i) => Embed(
        title: "🗺️ CS2.KR 맵 변경",
        color: 0x3498DB,
        description: $"`{i.Map}` 로 맵을 변경합니다.",
        fields: new[]
        {
            Field("실행자", AdminLink(i.AdminName, i.AdminSteamId), true),
        });

    // ───────────────────────── 헬퍼 ─────────────────────────

    private static string SteamUrl(string steamId64) => $"https://steamcommunity.com/profiles/{steamId64}";

    /// <summary>대상 — 닉네임 클릭하면 Steam 프로필 + SteamID64 별도 라인.</summary>
    private static string PlayerLink(string? name, string steamId64)
    {
        var display = string.IsNullOrWhiteSpace(name) ? steamId64 : name;
        return $"**[{Escape(display)}]({SteamUrl(steamId64)})**\n`{steamId64}`";
    }

    /// <summary>발급자/해제자 — 닉네임만 클릭 가능, SteamID 미표시.</summary>
    private static string AdminLink(string? name, string steamId64)
    {
        var display = string.IsNullOrWhiteSpace(name) ? steamId64 : name;
        return $"[{Escape(display)}]({SteamUrl(steamId64)})";
    }

    private record struct EmbedField(string Name, string Value, bool Inline);

    private static EmbedField Field(string name, string value, bool inline) => new(name, value, inline);

    private static string DurationText(int minutes)
    {
        if (minutes <= 0) return "**영구**";
        if (minutes < 60) return $"{minutes}분";
        if (minutes < 1440) return $"{minutes / 60}시간 {(minutes % 60 == 0 ? "" : $"{minutes % 60}분")}".TrimEnd();
        if (minutes < 10080) return $"{minutes / 1440}일";
        if (minutes < 43200) return $"{minutes / 10080}주";
        return $"{minutes / 43200}개월";
    }

    private static string Escape(string s) => s.Replace("[", "\\[").Replace("]", "\\]");

    private static string Trim(string s, int max) => s.Length > max ? s[..max] + "…" : s;

    private object Embed(
        string title,
        int color,
        string? description = null,
        string? url = null,
        string? thumbnailUrl = null,
        EmbedField[]? fields = null)
    {
        // description: max 4096
        if (description != null && description.Length > 4090)
            description = description[..4090] + "…";

        // field.value: max 1024
        var safeFields = (fields ?? Array.Empty<EmbedField>())
            .Select(f => new
            {
                name = f.Name,
                value = f.Value.Length > 1020 ? f.Value[..1020] + "…" : f.Value,
                inline = f.Inline,
            })
            .ToArray();

        return new
        {
            title = title.Length > 250 ? title[..250] : title,
            color,
            url,
            description,
            thumbnail = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : new { url = thumbnailUrl },
            fields = safeFields,
            timestamp = DateTimeOffset.UtcNow.ToOffset(KST_OFFSET).ToString("o"),
            footer = new { text = BRAND_NAME },
        };
    }

    // ───────────────────────── 송신 ─────────────────────────

    private void Send(object embed)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.WebhookUrl)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var payload = new { embeds = new[] { embed } };
                using var resp = await _http.PostAsJsonAsync(_config.WebhookUrl, payload);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning("Discord webhook {Code}: {Body}", (int)resp.StatusCode, body);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Discord webhook 전송 실패");
            }
        });
    }
}
