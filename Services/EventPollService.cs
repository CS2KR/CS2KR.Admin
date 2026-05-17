using System.Text.Json;
using CounterStrikeSharp.API;
using CS2KR.Admin.Models;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

/// <summary>
/// sa_admin_events 를 짧은 간격으로 폴링하여 새 이벤트를 즉시 처리.
/// 메모리 _cursor 로 last seen id 추적. 모든 게임 객체 접근은 Server.NextFrame.
///
/// Discord 발송 중복 방지: 다중 서버 환경에서 동일 이벤트로 N번 발송되는 문제를
/// EventRepository.TryClaimDiscordAsync 의 atomic UPDATE 로 해결 — 한 플러그인만 발송 성공.
/// </summary>
public sealed class EventPollService
{
    private readonly CS2KRAdminPlugin _plugin;
    private long _cursor;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public EventPollService(CS2KRAdminPlugin plugin) => _plugin = plugin;

    public async Task InitializeCursorAsync()
    {
        _cursor = await _plugin.EventRepo.GetMaxIdAsync();
        _plugin.Logger.LogInformation("이벤트 커서 초기화: {Cursor}", _cursor);
    }

    public void Start()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var interval = TimeSpan.FromSeconds(Math.Clamp(_plugin.Config.PollIntervalSeconds, 0.5, 10.0));

        _loop = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try { await PollOnceAsync(); }
                catch (Exception e) { _plugin.Logger.LogError(e, "이벤트 폴링 실패"); }
                try { await Task.Delay(interval, ct); } catch (TaskCanceledException) { break; }
            }
        }, ct);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* 무시 */ }
    }

    private async Task PollOnceAsync()
    {
        var events = await _plugin.EventRepo.FetchSinceAsync(_cursor, _plugin.ServerId);
        if (events.Count == 0) return;

        foreach (var ev in events)
        {
            try { await HandleAsync(ev); }
            catch (Exception e) { _plugin.Logger.LogError(e, "이벤트 처리 실패 id={Id} type={T}", ev.Id, ev.EventType); }
            _cursor = ev.Id;
        }
    }

    private async Task HandleAsync(AdminEvent ev)
    {
        var payload = ParsePayload(ev.RawPayload);
        var reason = Get(payload, "reason") ?? "";
        var muteType = Get(payload, "type");
        var adminName = Get(payload, "admin_name") ?? "Unknown";
        var adminSteamId = Get(payload, "admin_steamid") ?? "0";
        var targetName = Get(payload, "target_name");
        var duration = GetInt(payload, "duration");

        // 1) 인게임 enforcement (메인스레드에서)
        switch (ev.EventType)
        {
            case "ban":
                if (ulong.TryParse(ev.TargetSteamId, out var bsid))
                    Server.NextFrame(() => _plugin.Enforcement.KickIfPresent(bsid, reason));
                break;
            case "mute":
                if (ulong.TryParse(ev.TargetSteamId, out var msid) && !string.IsNullOrEmpty(muteType))
                    Server.NextFrame(() => _plugin.Enforcement.ApplyMute(msid, muteType));
                break;
            case "unmute":
                if (ulong.TryParse(ev.TargetSteamId, out var umsid))
                    Server.NextFrame(() => _plugin.Enforcement.RemoveMute(umsid, muteType));
                break;
            case "kick":
                if (ulong.TryParse(ev.TargetSteamId, out var ksid))
                    Server.NextFrame(() => _plugin.Enforcement.KickIfPresent(ksid, reason));
                break;
            case "reload_admins":
                await _plugin.Permissions.ReloadAsync();
                break;
        }

        // 2) Discord 발신 — 중복 방지를 위해 atomic claim. 한 플러그인만 true.
        if (!await _plugin.EventRepo.TryClaimDiscordAsync(ev.Id))
            return;

        var serverName = await ResolveServerNameAsync(ev.ServerId);
        var targetSid = ev.TargetSteamId ?? "0";
        var targetAvatar = Get(payload, "target_avatar");

        switch (ev.EventType)
        {
            case "ban":
                _plugin.Discord.SendBan(new DiscordWebhookService.BanInfo(
                    targetSid, targetName, targetAvatar, adminSteamId, adminName, reason, duration, serverName));
                break;
            case "unban":
                _plugin.Discord.SendUnban(new DiscordWebhookService.UnbanInfo(
                    targetSid, targetName, targetAvatar, adminSteamId, adminName, reason, serverName));
                break;
            case "ban_edit":
                _plugin.Discord.SendBanEdit(new DiscordWebhookService.BanEditInfo(
                    targetSid, targetName, targetAvatar, adminSteamId, adminName, reason, duration, serverName));
                break;
            case "mute":
                _plugin.Discord.SendMute(new DiscordWebhookService.MuteInfo(
                    targetSid, targetName, targetAvatar, adminSteamId, adminName, reason, duration,
                    muteType ?? "MUTE", serverName));
                break;
            case "unmute":
            case "mute_edit":
                _plugin.Discord.SendUnmute(new DiscordWebhookService.UnmuteInfo(
                    targetSid, targetName, targetAvatar, adminSteamId, adminName, reason, muteType, serverName));
                break;
            case "kick":
                _plugin.Discord.SendKick(new DiscordWebhookService.KickInfo(
                    targetSid, targetName, targetAvatar, adminSteamId, adminName, reason, serverName));
                break;
            default:
                _plugin.Logger.LogDebug("Discord 미지원 이벤트 타입: {T}", ev.EventType);
                break;
        }
    }

    private async Task<string?> ResolveServerNameAsync(int? serverId)
    {
        if (serverId == null) return null;
        try { return await _plugin.ServerRepo.GetHostnameByIdAsync(serverId.Value); }
        catch { return null; }
    }

    // ───── payload 헬퍼 ─────

    private static string? Get(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static int GetInt(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return 0;
        return v switch
        {
            int i => i,
            long l => (int)l,
            double dd => (int)dd,
            string s => int.TryParse(s, out var p) ? p : 0,
            _ => 0,
        };
    }

    private static Dictionary<string, object?> ParsePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.TryGetInt64(out var n) ? n : p.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => p.Value.GetRawText(),
                };
            }
            return dict;
        }
        catch { return new(); }
    }
}
