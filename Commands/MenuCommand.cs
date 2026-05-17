using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2KR.Admin.Menus;

namespace CS2KR.Admin.Commands;

public static class MenuCommand
{
    public static void Register(CS2KRAdminPlugin plugin)
    {
        plugin.AddCommand("css_admin", "어드민 메뉴 열기", (c, i) => OnAdminMenu(plugin, c, i));
    }

    private static void OnAdminMenu(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (caller == null || !caller.IsValid)
        {
            CommandHelpers.Reply(info, plugin, "인게임에서만 사용 가능합니다.");
            return;
        }

        var a = plugin.Permissions.Get(caller);
        if (a == null)
        {
            CommandHelpers.Reply(info, plugin, "어드민 권한이 없습니다.");
            return;
        }

        AdminMenu.Open(plugin, caller);
    }
}
