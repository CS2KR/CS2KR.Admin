using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2KR.Admin.Commands;

public static class ChatCommands
{
    public static void Register(CS2KRAdminPlugin plugin)
    {
        plugin.AddCommand("css_say",  "전체 채팅 메시지", (c, i) => OnSay(plugin, c, i));
        plugin.AddCommand("css_csay", "센터 메시지",      (c, i) => OnCsay(plugin, c, i));
        plugin.AddCommand("css_psay", "개인 메시지",      (c, i) => OnPsay(plugin, c, i));
    }

    private static void OnSay(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/chat", info)) return;
        if (info.ArgCount < 2) { CommandHelpers.Reply(info, plugin, "사용법: css_say <메시지>"); return; }
        var msg = string.Join(' ', Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)));
        Server.PrintToChatAll($"{plugin.Config.ChatPrefix}[관리자] {msg}");
    }

    private static void OnCsay(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/chat", info)) return;
        if (info.ArgCount < 2) { CommandHelpers.Reply(info, plugin, "사용법: css_csay <메시지>"); return; }
        var msg = string.Join(' ', Enumerable.Range(1, info.ArgCount - 1).Select(i => info.GetArg(i)));
        foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
            p.PrintToCenter(msg);
    }

    private static void OnPsay(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/chat", info)) return;
        if (info.ArgCount < 3) { CommandHelpers.Reply(info, plugin, "사용법: css_psay <대상> <메시지>"); return; }
        var targets = CommandHelpers.ResolveTargets(info.GetArg(1), caller);
        if (targets.Count == 0) { CommandHelpers.Reply(info, plugin, "대상을 찾을 수 없습니다."); return; }
        var msg = string.Join(' ', Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)));
        foreach (var t in targets)
            t.PrintToChat($"{plugin.Config.ChatPrefix}[{CommandHelpers.AdminName(caller)}] {msg}");
    }
}
