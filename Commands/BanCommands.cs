using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Commands;

public static class BanCommands
{
    public static void Register(CS2KRAdminPlugin plugin)
    {
        plugin.AddCommand("css_ban", "플레이어 영구/기간 밴", (caller, info) => OnBan(plugin, caller, info));
        plugin.AddCommand("css_unban", "SteamID 언밴", (caller, info) => OnUnban(plugin, caller, info));
    }

    private static void OnBan(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/ban", info)) return;
        if (info.ArgCount < 4)
        {
            CommandHelpers.Reply(info, plugin, "사용법: css_ban <대상> <분(0=영구)> <사유>");
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
            var pIp = target.IpAddress?.Split(':')[0];

            _ = Task.Run(async () =>
            {
                try
                {
                    var banId = await plugin.BanRepo.InsertAsync(pSid, pName, pIp, reason, minutes, adminSid, adminName, null);

                    await plugin.EventRepo.EmitAsync("ban", pSid, banId,
                        CommandHelpers.PayloadJson(new
                        {
                            reason, duration = minutes,
                            target_name = pName,
                            admin_steamid = adminSid, admin_name = adminName, source = "ingame",
                        }), null);

                    Server.NextFrame(() => plugin.Enforcement.KickIfPresent(target.SteamID, reason));

                    Server.NextFrame(() =>
                    {
                        if (plugin.Config.BroadcastBans)
                            CommandHelpers.Broadcast(plugin, $"{adminName} 님이 {pName} 을(를) 밴했습니다. 사유: {reason}");
                    });
                }
                catch (Exception e)
                {
                    plugin.Logger.LogError(e, "css_ban DB 실패");
                    Server.NextFrame(() => CommandHelpers.Reply(info, plugin, $"DB 오류: {e.Message}"));
                }
            });
        }
    }

    private static void OnUnban(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/unban", info)) return;
        if (info.ArgCount < 2)
        {
            CommandHelpers.Reply(info, plugin, "사용법: css_unban <steamid> [사유]");
            return;
        }

        var steamid = info.GetArg(1).Trim();
        var reason = info.ArgCount >= 3
            ? string.Join(' ', Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)))
            : "관리자에 의한 해제";
        var adminSid = CommandHelpers.AdminSteamId(caller);
        var adminName = CommandHelpers.AdminName(caller);

        _ = Task.Run(async () =>
        {
            try
            {
                await plugin.BanRepo.UnbanBySteamIdAsync(steamid, adminSid, reason);
                await plugin.EventRepo.EmitAsync("unban", steamid, null,
                    CommandHelpers.PayloadJson(new { reason, admin_steamid = adminSid, admin_name = adminName, source = "ingame" }), null);

                Server.NextFrame(() => CommandHelpers.Reply(info, plugin, $"{steamid} 언밴 완료."));
            }
            catch (Exception e)
            {
                plugin.Logger.LogError(e, "css_unban DB 실패");
                Server.NextFrame(() => CommandHelpers.Reply(info, plugin, $"DB 오류: {e.Message}"));
            }
        });
    }
}
