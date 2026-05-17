using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CS2KR.Admin.Commands;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Menus;

public static class ReasonMenu
{
    public static void OpenBan(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target, int minutes)
    {
        var menu = new CenterHtmlMenu($"{target.PlayerName} - 밴 사유", plugin);
        foreach (var reason in plugin.Config.BanReasons)
        {
            var r = reason;
            menu.AddMenuOption(reason, (p, _) => ExecuteBan(plugin, p, target, minutes, r));
        }
        menu.AddMenuOption("뒤로", (p, _) => DurationMenu.OpenBan(plugin, p, target));
        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }

    public static void OpenMute(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target, string type, int minutes)
    {
        var menu = new CenterHtmlMenu($"{target.PlayerName} - 사유", plugin);
        foreach (var reason in plugin.Config.MuteReasons)
        {
            var r = reason;
            menu.AddMenuOption(reason, (p, _) => ExecuteMute(plugin, p, target, type, minutes, r));
        }
        menu.AddMenuOption("뒤로", (p, _) => DurationMenu.OpenMute(plugin, p, target, type));
        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }

    public static void OpenKick(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target)
    {
        var menu = new CenterHtmlMenu($"{target.PlayerName} - 킥 사유", plugin);
        foreach (var reason in plugin.Config.BanReasons)
        {
            var r = reason;
            menu.AddMenuOption(reason, (p, _) => ExecuteKick(plugin, p, target, r));
        }
        menu.AddMenuOption("뒤로", (p, _) => ActionMenu.Open(plugin, p, target));
        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }

    private static void ExecuteBan(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target, int minutes, string reason)
    {
        if (!plugin.Permissions.CanTarget(caller, target))
        {
            caller.PrintToChat($"{plugin.Config.ChatPrefix}면역 등급 부족.");
            return;
        }

        var pSid = target.SteamID.ToString();
        var pName = target.PlayerName ?? "Unknown";
        var pIp = target.IpAddress?.Split(':')[0];
        var adminSid = caller.SteamID.ToString();
        var adminName = caller.PlayerName ?? "Unknown";
        var tSidUlong = target.SteamID;

        _ = Task.Run(async () =>
        {
            try
            {
                var banId = await plugin.BanRepo.InsertAsync(pSid, pName, pIp, reason, minutes, adminSid, adminName, null);
                await plugin.EventRepo.EmitAsync("ban", pSid, banId,
                    CommandHelpers.PayloadJson(new { reason, duration = minutes, admin_steamid = adminSid, admin_name = adminName, source = "ingame" }), null);

                Server.NextFrame(() => plugin.Enforcement.KickIfPresent(tSidUlong, reason));

                plugin.Discord.Send("ban", new()
                {
                    ["대상"] = $"{pName} ({pSid})", ["사유"] = reason,
                    ["기간"] = minutes == 0 ? "영구" : $"{minutes}분",
                    ["발급자"] = adminName, ["출처"] = "인게임",
                });

                Server.NextFrame(() => Server.PrintToChatAll($"{plugin.Config.ChatPrefix}{adminName} 님이 {pName} 을(를) 밴했습니다. 사유: {reason}"));
            }
            catch (Exception e) { plugin.Logger.LogError(e, "menu ban 실패"); }
        });
    }

    private static void ExecuteMute(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target, string type, int minutes, string reason)
    {
        if (!plugin.Permissions.CanTarget(caller, target))
        {
            caller.PrintToChat($"{plugin.Config.ChatPrefix}면역 등급 부족.");
            return;
        }

        var pSid = target.SteamID.ToString();
        var pName = target.PlayerName ?? "Unknown";
        var adminSid = caller.SteamID.ToString();
        var adminName = caller.PlayerName ?? "Unknown";
        var tSidUlong = target.SteamID;

        _ = Task.Run(async () =>
        {
            try
            {
                var muteId = await plugin.MuteRepo.InsertAsync(pSid, pName, reason, minutes, type, adminSid, adminName, null);
                await plugin.EventRepo.EmitAsync("mute", pSid, muteId,
                    CommandHelpers.PayloadJson(new { reason, duration = minutes, type, admin_steamid = adminSid, admin_name = adminName, source = "ingame" }), null);

                Server.NextFrame(() => plugin.Enforcement.ApplyMute(tSidUlong, type));

                plugin.Discord.Send("mute", new()
                {
                    ["대상"] = $"{pName} ({pSid})", ["종류"] = type, ["사유"] = reason,
                    ["기간"] = minutes == 0 ? "영구" : $"{minutes}분",
                    ["발급자"] = adminName, ["출처"] = "인게임",
                });

                Server.NextFrame(() => Server.PrintToChatAll($"{plugin.Config.ChatPrefix}{adminName} 님이 {pName} 을(를) 차단했습니다."));
            }
            catch (Exception e) { plugin.Logger.LogError(e, "menu mute 실패"); }
        });
    }

    private static void ExecuteKick(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target, string reason)
    {
        if (!plugin.Permissions.CanTarget(caller, target))
        {
            caller.PrintToChat($"{plugin.Config.ChatPrefix}면역 등급 부족.");
            return;
        }

        var pSid = target.SteamID.ToString();
        var pName = target.PlayerName ?? "Unknown";
        var adminSid = caller.SteamID.ToString();
        var adminName = caller.PlayerName ?? "Unknown";
        var uid = target.UserId ?? 0;

        Server.ExecuteCommand($"kickid {uid} \"{reason.Replace("\"", "'")}\"");

        _ = Task.Run(async () =>
        {
            try
            {
                await plugin.EventRepo.EmitAsync("kick", pSid, null,
                    CommandHelpers.PayloadJson(new { reason, admin_steamid = adminSid, admin_name = adminName, source = "ingame" }), null);

                plugin.Discord.Send("kick", new()
                {
                    ["대상"] = $"{pName} ({pSid})", ["사유"] = reason,
                    ["발급자"] = adminName, ["출처"] = "인게임",
                });
            }
            catch (Exception e) { plugin.Logger.LogWarning(e, "menu kick 이벤트 실패"); }
        });
    }
}
