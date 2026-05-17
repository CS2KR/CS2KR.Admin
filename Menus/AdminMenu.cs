using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace CS2KR.Admin.Menus;

/// <summary>
/// 어드민 최상위 메뉴 (CenterHtmlMenu).
/// 플레이어 관리 / 서버 관리 / 어드민 정보.
/// </summary>
public static class AdminMenu
{
    public static void Open(CS2KRAdminPlugin plugin, CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("CS2KR 관리 메뉴", plugin);

        menu.AddMenuOption("플레이어 관리", (p, opt) => PlayerSelectMenu.Open(plugin, p));
        menu.AddMenuOption("어드민 목록", (p, opt) =>
        {
            var lines = plugin.Permissions.AllAdmins()
                .Take(20)
                .Select(a => $"{a.Name} ({a.GroupName ?? "-"})")
                .ToList();
            if (lines.Count == 0) p.PrintToChat($"{plugin.Config.ChatPrefix}등록된 어드민이 없습니다.");
            else foreach (var l in lines) p.PrintToChat($"{plugin.Config.ChatPrefix}{l}");
        });
        menu.AddMenuOption("권한 캐시 리로드", (p, opt) =>
        {
            Task.Run(async () =>
            {
                await plugin.Permissions.ReloadAsync();
                Server.NextFrame(() => p.PrintToChat($"{plugin.Config.ChatPrefix}어드민 캐시 리로드 완료."));
            });
        });
        menu.AddMenuOption("닫기", (p, opt) => MenuManager.CloseActiveMenu(player));

        MenuManager.OpenCenterHtmlMenu(plugin, player, menu);
    }
}
