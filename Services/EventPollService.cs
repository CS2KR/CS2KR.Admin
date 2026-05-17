using System.Text.Json;
using CounterStrikeSharp.API;
using CS2KR.Admin.Models;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

/// <summary>
/// sa_admin_events 를 짧은 간격으로 폴링하여 새 이벤트를 즉시 처리.
/// 메모리 _cursor 로 last seen id 추적. 모든 게임 객체 접근은 Server.NextFrame.
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
                try
                {
                    await PollOnceAsync();
                }
                catch (Exception e)
                {
                    _plugin.Logger.LogError(e, "이벤트 폴링 실패");
                }
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
            try
            {
                await HandleAsync(ev);
            }
            catch (Exception e)
            {
                _plugin.Logger.LogError(e, "이벤트 처리 실패 id={Id} type={T}", ev.Id, ev.EventType);
            }
            _cursor = ev.Id;
        }
    }

    private async Task HandleAsync(AdminEvent ev)
    {
        var payload = ParsePayload(ev.RawPayload);
        var source = payload.TryGetValue("source", out var s) ? s?.ToString() : null;
        var reason = payload.TryGetValue("reason", out var r) ? r?.ToString() ?? "" : "";
        var muteType = payload.TryGetValue("type", out var t) ? t?.ToString() : null;
        var adminName = payload.TryGetValue("admin_name", out var an) ? an?.ToString() ?? "" : "";
        var adminSteamId = payload.TryGetValue("admin_steamid", out var asi) ? asi?.ToString() ?? "" : "";
        var duration = payload.TryGetValue("duration", out var d) && d != null ? d.ToString() : "0";

        switch (ev.EventType)
        {
            case "ban":
                if (ulong.TryParse(ev.TargetSteamId, out var bsid))
                    Server.NextFrame(() => _plugin.Enforcement.KickIfPresent(bsid, reason));
                if (source == "web")
                    _plugin.Discord.Send("ban", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["사유"] = reason, ["기간(분)"] = duration,
                        ["발급자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "unban":
                if (source == "web")
                    _plugin.Discord.Send("unban", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["사유"] = reason,
                        ["해제자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "ban_edit":
                if (source == "web")
                    _plugin.Discord.Send("ban_edit", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["사유"] = reason, ["기간(분)"] = duration,
                        ["수정자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "mute":
                if (ulong.TryParse(ev.TargetSteamId, out var msid) && !string.IsNullOrEmpty(muteType))
                    Server.NextFrame(() => _plugin.Enforcement.ApplyMute(msid, muteType));
                if (source == "web")
                    _plugin.Discord.Send("mute", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["종류"] = muteType,
                        ["사유"] = reason, ["기간(분)"] = duration,
                        ["발급자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "unmute":
                if (ulong.TryParse(ev.TargetSteamId, out var umsid))
                    Server.NextFrame(() => _plugin.Enforcement.RemoveMute(umsid, muteType));
                if (source == "web")
                    _plugin.Discord.Send("unmute", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["종류"] = muteType ?? "전체",
                        ["사유"] = reason, ["해제자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "mute_edit":
                if (source == "web")
                    _plugin.Discord.Send("mute_edit", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["종류"] = muteType,
                        ["사유"] = reason, ["기간(분)"] = duration,
                        ["수정자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "kick":
                if (ulong.TryParse(ev.TargetSteamId, out var ksid))
                    Server.NextFrame(() => _plugin.Enforcement.KickIfPresent(ksid, reason));
                if (source == "web")
                    _plugin.Discord.Send("kick", new()
                    {
                        ["대상"] = ev.TargetSteamId, ["사유"] = reason,
                        ["발급자"] = adminName, ["출처"] = "웹",
                    });
                break;

            case "reload_admins":
                await _plugin.Permissions.ReloadAsync();
                break;

            default:
                _plugin.Logger.LogDebug("알 수 없는 이벤트 타입: {T}", ev.EventType);
                break;
        }
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
