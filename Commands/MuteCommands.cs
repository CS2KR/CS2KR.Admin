using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Commands;

public static class MuteCommands
{
    public static void Register(CS2KRAdminPlugin plugin)
    {
        plugin.AddCommand("css_mute",   "음성 차단",            (c, i) => Issue(plugin, c, i, "MUTE"));
        plugin.AddCommand("css_gag",    "채팅 차단",            (c, i) => Issue(plugin, c, i, "GAG"));
        plugin.AddCommand("css_silence","음성+채팅 차단",       (c, i) => Issue(plugin, c, i, "SILENCE"));
        plugin.AddCommand("css_unmute", "음성 차단 해제",       (c, i) => Remove(plugin, c, i, "MUTE"));
        plugin.AddCommand("css_ungag",  "채팅 차단 해제",       (c, i) => Remove(plugin, c, i, "GAG"));
        plugin.AddCommand("css_unsilence","음성+채팅 차단 해제",(c, i) => Remove(plugin, c, i, "SILENCE"));
    }

    private static void Issue(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info, string type)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/chat", info)) return;
        if (info.ArgCount < 4)
        {
            CommandHelpers.Reply(info, plugin, $"사용법: {info.GetArg(0)} <대상> <분(0=영구)> <사유>");
            return;
        }

        var token = info.GetArg(1);
        if (!int.TryParse(info.GetArg(2), out var minutes) || minutes < 0)
        {
            CommandHelpers.Reply(info, plugin, "기간(분) 은 0 이상의 정수여야 합니다.");
            return;
        }
        var reason = string.Join(' ', Enumerable.Range(3, info.ArgCount - 3).Select(i => info.GetArg(i)));

        var targets = CommandHelpers.ResolveTargets(token, caller);
        if (targets.Count == 0)
        {
            CommandHelpers.Reply(info, plugin, $"대상을 찾을 수 없습니다: {token}");
            return;
        }

        var adminSid = CommandHelpers.AdminSteamId(caller);
        var adminName = CommandHelpers.AdminName(caller);

        foreach (var target in targets)
        {
            if (!CommandHelpers.RequireCanTarget(plugin, caller, target, info)) continue;

            var pSid = target.SteamID.ToString();
            var pName = target.PlayerName ?? "Unknown";

            _ = Task.Run(async () =>
            {
                try
                {
                    var muteId = await plugin.MuteRepo.InsertAsync(pSid, pName, reason, minutes, type, adminSid, adminName, null);

                    await plugin.EventRepo.EmitAsync("mute", pSid, muteId,
                        CommandHelpers.PayloadJson(new
                        {
                            reason, duration = minutes, type,
                            target_name = pName,
                            admin_steamid = adminSid, admin_name = adminName, source = "ingame",
                        }), null);

                    Server.NextFrame(() => plugin.Enforcement.ApplyMute(target.SteamID, type));

                    Server.NextFrame(() => CommandHelpers.Broadcast(plugin, $"{adminName} 님이 {pName} 을(를) {Korean(type)}했습니다."));
                }
                catch (Exception e)
                {
                    plugin.Logger.LogError(e, "mute 명령 DB 실패");
                    Server.NextFrame(() => CommandHelpers.Reply(info, plugin, $"DB 오류: {e.Message}"));
                }
            });
        }
    }

    private static void Remove(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info, string type)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/chat", info)) return;
        if (info.ArgCount < 2)
        {
            CommandHelpers.Reply(info, plugin, $"사용법: {info.GetArg(0)} <steamid_또는_이름> [사유]");
            return;
        }

        var token = info.GetArg(1).Trim();
        var reason = info.ArgCount >= 3
            ? string.Join(' ', Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)))
            : "관리자에 의한 해제";

        // 타겟 해석 — SteamID64 직접 또는 이름
        string? steamId = null;
        if (ulong.TryParse(token, out var sid64) && sid64 > 76561197960265728UL)
            steamId = sid64.ToString();
        else
        {
            var matched = CommandHelpers.ResolveTargets(token, caller).FirstOrDefault();
            if (matched != null) steamId = matched.SteamID.ToString();
        }
        if (steamId == null)
        {
            CommandHelpers.Reply(info, plugin, $"대상을 찾을 수 없습니다: {token}");
            return;
        }

        var adminSid = CommandHelpers.AdminSteamId(caller);
        var adminName = CommandHelpers.AdminName(caller);
        var sidLocal = steamId;

        _ = Task.Run(async () =>
        {
            try
            {
                await plugin.MuteRepo.UnmuteBySteamIdAsync(sidLocal, adminSid, reason, type);

                await plugin.EventRepo.EmitAsync("unmute", sidLocal, null,
                    CommandHelpers.PayloadJson(new { reason, type, admin_steamid = adminSid, admin_name = adminName, source = "ingame" }), null);

                if (ulong.TryParse(sidLocal, out var ul))
                    Server.NextFrame(() => plugin.Enforcement.RemoveMute(ul, type));

                Server.NextFrame(() => CommandHelpers.Reply(info, plugin, $"{sidLocal} {Korean(type)} 해제 완료."));
            }
            catch (Exception e)
            {
                plugin.Logger.LogError(e, "unmute DB 실패");
                Server.NextFrame(() => CommandHelpers.Reply(info, plugin, $"DB 오류: {e.Message}"));
            }
        });
    }

    private static string Korean(string type) => type switch
    {
        "GAG" => "채팅 차단",
        "MUTE" => "음성 차단",
        "SILENCE" => "전체 차단",
        _ => type,
    };
}
