using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2KR.Admin.Services;

namespace CS2KR.Admin.Commands;

public static class ServerCommands
{
    public static void Register(CS2KRAdminPlugin plugin)
    {
        plugin.AddCommand("css_changemap", "맵 변경",   (c, i) => OnMap(plugin, c, i));
        plugin.AddCommand("css_map",       "맵 변경",   (c, i) => OnMap(plugin, c, i));
        plugin.AddCommand("css_rcon",      "RCON 실행", (c, i) => OnRcon(plugin, c, i));
        plugin.AddCommand("css_who",       "어드민 목록(접속 중)", (c, i) => OnWho(plugin, c, i));
        plugin.AddCommand("css_admins",    "어드민 목록(전체)",   (c, i) => OnAdmins(plugin, c, i));
        plugin.AddCommand("css_reload_admins", "어드민 캐시 리로드", (c, i) => OnReloadAdmins(plugin, c, i));
        plugin.AddCommand("css_reload_bans",   "온라인 플레이어 밴/뮤트 재확인", (c, i) => OnReloadBans(plugin, c, i));
    }

    private static void OnMap(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/changemap", info)) return;
        if (info.ArgCount < 2) { CommandHelpers.Reply(info, plugin, "사용법: css_map <맵이름>"); return; }
        var map = info.GetArg(1).Trim();
        var adminName = CommandHelpers.AdminName(caller);
        var adminSid = CommandHelpers.AdminSteamId(caller);

        CommandHelpers.Broadcast(plugin, $"{adminName} 님이 맵을 {map} 으로 변경합니다.");
        Server.ExecuteCommand($"changelevel {map}");

        plugin.Discord.SendMapChange(new DiscordWebhookService.MapInfo(
            adminSid, adminName, map, plugin.ServerName));
    }

    private static void OnRcon(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/rcon", info)) return;
        if (info.ArgCount < 2) { CommandHelpers.Reply(info, plugin, "사용법: css_rcon <명령>"); return; }
        var cmd = string.Join(' ', Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)));
        var adminName = CommandHelpers.AdminName(caller);
        var adminSid = CommandHelpers.AdminSteamId(caller);

        Server.ExecuteCommand(cmd);
        CommandHelpers.Reply(info, plugin, $"RCON 실행: {cmd}");

        plugin.Discord.SendRcon(new DiscordWebhookService.RconInfo(
            adminSid, adminName, cmd, plugin.ServerName));
    }

    private static void OnWho(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        var online = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && plugin.Permissions.Get(p) != null)
            .Select(p =>
            {
                var a = plugin.Permissions.Get(p)!;
                return $"{p.PlayerName} ({a.GroupName ?? "-"}, immunity={a.Immunity})";
            })
            .ToList();

        if (online.Count == 0) CommandHelpers.Reply(info, plugin, "현재 접속한 어드민이 없습니다.");
        else foreach (var line in online) CommandHelpers.Reply(info, plugin, line);
    }

    private static void OnAdmins(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        var list = plugin.Permissions.AllAdmins().Take(50).ToList();
        if (list.Count == 0) CommandHelpers.Reply(info, plugin, "등록된 어드민이 없습니다.");
        else
        {
            CommandHelpers.Reply(info, plugin, $"등록된 어드민 {list.Count}명:");
            foreach (var a in list) CommandHelpers.Reply(info, plugin, $"  {a.Name} [{a.GroupName ?? "-"}] immunity={a.Immunity}");
        }
    }

    private static void OnReloadAdmins(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/root", info)) return;
        _ = Task.Run(async () =>
        {
            await plugin.Permissions.ReloadAsync();
            Server.NextFrame(() => CommandHelpers.Reply(info, plugin, "어드민 캐시 리로드 완료."));
        });
    }

    private static void OnReloadBans(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/root", info)) return;
        // 메인스레드에서 SteamID 수집
        var sids = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot).Select(p => p.SteamID).ToList();
        Task.Run(async () =>
        {
            await plugin.Enforcement.ApplyToOnlineAsync(sids);
            Server.NextFrame(() => CommandHelpers.Reply(info, plugin, "온라인 플레이어 밴/뮤트 재확인 완료."));
        });
    }
}
