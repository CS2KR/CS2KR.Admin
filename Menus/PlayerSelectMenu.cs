using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace CS2KR.Admin.Menus;

public static class PlayerSelectMenu
{
    public static void Open(CS2KRAdminPlugin plugin, CCSPlayerController caller)
    {
        var menu = new CenterHtmlMenu("플레이어 선택", plugin);
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && p.UserId.HasValue).ToList();

        if (players.Count == 0)
        {
            caller.PrintToChat($"{plugin.Config.ChatPrefix}접속한 플레이어가 없습니다.");
            return;
        }

        foreach (var target in players)
        {
            var label = $"{target.PlayerName}";
            menu.AddMenuOption(label, (p, _) => ActionMenu.Open(plugin, p, target));
        }

        menu.AddMenuOption("뒤로", (p, _) => AdminMenu.Open(plugin, p));
        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }
}

public static class ActionMenu
{
    public static void Open(CS2KRAdminPlugin plugin, CCSPlayerController caller, CCSPlayerController target)
    {
        if (!plugin.Permissions.CanTarget(caller, target))
        {
            caller.PrintToChat($"{plugin.Config.ChatPrefix}{target.PlayerName} 은(는) 면역 등급이 더 높습니다.");
            return;
        }

        var menu = new CenterHtmlMenu($"{target.PlayerName} - 액션", plugin);
        var targetSid = target.SteamID;

        menu.AddMenuOption("밴", (p, _) => DurationMenu.OpenBan(plugin, p, target));
        menu.AddMenuOption("킥", (p, _) => ReasonMenu.OpenKick(plugin, p, target));
        menu.AddMenuOption("채팅 차단 (gag)", (p, _) => DurationMenu.OpenMute(plugin, p, target, "GAG"));
        menu.AddMenuOption("음성 차단 (mute)", (p, _) => DurationMenu.OpenMute(plugin, p, target, "MUTE"));
        menu.AddMenuOption("전체 차단 (silence)", (p, _) => DurationMenu.OpenMute(plugin, p, target, "SILENCE"));
        menu.AddMenuOption("슬레이", (p, _) =>
        {
            try { target.PlayerPawn.Value?.CommitSuicide(true, true); } catch { }
            Server.PrintToChatAll($"{plugin.Config.ChatPrefix}{p.PlayerName} 님이 {target.PlayerName} 을(를) 슬레이했습니다.");
        });
        menu.AddMenuOption("슬랩 (10)", (p, _) =>
        {
            try
            {
                var pawn = target.PlayerPawn.Value;
                if (pawn != null)
                {
                    pawn.Health = Math.Max(1, pawn.Health - 10);
                    Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                    var rnd = new Random();
                    pawn.AbsVelocity.X = rnd.Next(-300, 300);
                    pawn.AbsVelocity.Y = rnd.Next(-300, 300);
                    pawn.AbsVelocity.Z = rnd.Next(150, 350);
                }
                Server.PrintToChatAll($"{plugin.Config.ChatPrefix}{p.PlayerName} 님이 {target.PlayerName} 을(를) 슬랩했습니다.");
            }
            catch { }
        });
        menu.AddMenuOption("뒤로", (p, _) => PlayerSelectMenu.Open(plugin, p));

        MenuManager.OpenCenterHtmlMenu(plugin, caller, menu);
    }
}
