using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace CS2KR.Admin.Menus;

public static class DurationMenu
{
    public static void OpenBan(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target)
    {
        var menu = new CenterHtmlMenu($"{target.PlayerName} - 밴 기간", plugin);
        foreach (var d in plugin.Config.BanDurations)
        {
            var minutes = d.Minutes;
            menu.AddMenuOption(d.Label, (p, _) => ReasonMenu.OpenBan(plugin, p, target, minutes));
        }
        menu.AddMenuOption("뒤로", (p, _) => ActionMenu.Open(plugin, p, target));
        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }

    public static void OpenMute(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target, string type)
    {
        var menu = new CenterHtmlMenu($"{target.PlayerName} - {Korean(type)} 기간", plugin);
        foreach (var d in plugin.Config.MuteDurations)
        {
            var minutes = d.Minutes;
            menu.AddMenuOption(d.Label, (p, _) => ReasonMenu.OpenMute(plugin, p, target, type, minutes));
        }
        menu.AddMenuOption("뒤로", (p, _) => ActionMenu.Open(plugin, p, target));
        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }

    private static string Korean(string type) => type switch
    {
        "GAG" => "채팅 차단",
        "MUTE" => "음성 차단",
        "SILENCE" => "전체 차단",
        _ => type,
    };
}
