using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Commands;

public static class PlayerCommands
{
    public static void Register(CS2KRAdminPlugin plugin)
    {
        plugin.AddCommand("css_kick", "플레이어 킥", (c, i) => OnKick(plugin, c, i));
        plugin.AddCommand("css_slay", "플레이어 자살", (c, i) => OnSlay(plugin, c, i));
        plugin.AddCommand("css_slap", "플레이어 슬랩 (데미지+속도)", (c, i) => OnSlap(plugin, c, i));
    }

    private static void OnKick(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/kick", info)) return;
        if (info.ArgCount < 2)
        {
            CommandHelpers.Reply(info, plugin, "사용법: css_kick <대상> [사유]");
            return;
        }

        var token = info.GetArg(1);
        var reason = info.ArgCount >= 3
            ? string.Join(' ', Enumerable.Range(2, info.ArgCount - 2).Select(i => info.GetArg(i)))
            : "관리자에 의한 킥";

        var targets = CommandHelpers.ResolveTargets(token, caller);
        if (targets.Count == 0) { CommandHelpers.Reply(info, plugin, $"대상을 찾을 수 없습니다: {token}"); return; }

        var adminSid = CommandHelpers.AdminSteamId(caller);
        var adminName = CommandHelpers.AdminName(caller);

        foreach (var target in targets)
        {
            if (!CommandHelpers.RequireCanTarget(plugin, caller, target, info)) continue;

            var pSid = target.SteamID.ToString();
            var pName = target.PlayerName ?? "Unknown";
            var uid = target.UserId ?? 0;

            Server.ExecuteCommand($"kickid {uid} \"{Sanitize(reason)}\"");

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
                catch (Exception e) { plugin.Logger.LogWarning(e, "kick 이벤트 발행 실패"); }
            });

            Server.NextFrame(() => CommandHelpers.Broadcast(plugin, $"{adminName} 님이 {pName} 을(를) 킥했습니다. 사유: {reason}"));
        }
    }

    private static void OnSlay(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/slay", info)) return;
        if (info.ArgCount < 2) { CommandHelpers.Reply(info, plugin, "사용법: css_slay <대상>"); return; }

        var targets = CommandHelpers.ResolveTargets(info.GetArg(1), caller);
        var adminName = CommandHelpers.AdminName(caller);
        foreach (var t in targets)
        {
            if (!CommandHelpers.RequireCanTarget(plugin, caller, t, info)) continue;
            try
            {
                t.PlayerPawn.Value?.CommitSuicide(true, true);
                CommandHelpers.Broadcast(plugin, $"{adminName} 님이 {t.PlayerName} 을(를) 슬레이했습니다.");
            }
            catch (Exception e) { plugin.Logger.LogWarning(e, "slay 실패"); }
        }
    }

    private static void OnSlap(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CommandInfo info)
    {
        if (!CommandHelpers.RequireFlag(plugin, caller, "@css/slay", info)) return;
        if (info.ArgCount < 2) { CommandHelpers.Reply(info, plugin, "사용법: css_slap <대상> [데미지]"); return; }

        var damage = 0;
        if (info.ArgCount >= 3 && int.TryParse(info.GetArg(2), out var d)) damage = Math.Max(0, d);

        var targets = CommandHelpers.ResolveTargets(info.GetArg(1), caller);
        var adminName = CommandHelpers.AdminName(caller);
        var rnd = new Random();

        foreach (var t in targets)
        {
            if (!CommandHelpers.RequireCanTarget(plugin, caller, t, info)) continue;
            try
            {
                var pawn = t.PlayerPawn.Value;
                if (pawn == null) continue;

                if (damage > 0)
                {
                    pawn.Health = Math.Max(1, pawn.Health - damage);
                    Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                }

                var vel = new Vector(
                    rnd.Next(-300, 300),
                    rnd.Next(-300, 300),
                    rnd.Next(150, 350)
                );
                pawn.AbsVelocity.X = vel.X;
                pawn.AbsVelocity.Y = vel.Y;
                pawn.AbsVelocity.Z = vel.Z;

                CommandHelpers.Broadcast(plugin, $"{adminName} 님이 {t.PlayerName} 을(를) 슬랩했습니다 ({damage} 데미지).");
            }
            catch (Exception e) { plugin.Logger.LogWarning(e, "slap 실패"); }
        }
    }

    private static string Sanitize(string s) => s.Replace("\"", "'").Replace("\n", " ");
}
