using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace CS2KR.Admin.Commands;

internal static class CommandHelpers
{
    /// <summary>
    /// 타겟 토큰 (#userid, 'name', steamid64, @me, @all, @ct, @t, @!me, @dead, @alive)을 플레이어 리스트로 해석.
    /// </summary>
    public static List<CCSPlayerController> ResolveTargets(string token, CCSPlayerController? caller)
    {
        token = token.Trim();
        if (string.IsNullOrWhiteSpace(token)) return new();

        var all = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot).ToList();

        switch (token.ToLowerInvariant())
        {
            case "@all": return all;
            case "@me": return caller == null ? new() : new() { caller };
            case "@!me": return all.Where(p => p.SteamID != caller?.SteamID).ToList();
            case "@ct": return all.Where(p => p.TeamNum == 3).ToList();
            case "@t": return all.Where(p => p.TeamNum == 2).ToList();
            case "@dead": return all.Where(p => !(p.PawnIsAlive)).ToList();
            case "@alive": return all.Where(p => p.PawnIsAlive).ToList();
        }

        if (token.StartsWith("#") && int.TryParse(token.AsSpan(1), out var uid))
        {
            return all.Where(p => p.UserId == uid).ToList();
        }

        if (ulong.TryParse(token, out var sid64) && sid64 > 76561197960265728UL)
        {
            return all.Where(p => p.SteamID == sid64).ToList();
        }

        var lower = token.ToLowerInvariant();
        return all.Where(p => (p.PlayerName ?? "").ToLowerInvariant().Contains(lower)).ToList();
    }

    public static void Reply(CommandInfo info, CS2KRAdminPlugin plugin, string msg)
    {
        var prefix = plugin.Config.ChatPrefix;
        info.ReplyToCommand($"{prefix}{msg}");
    }

    public static void Broadcast(CS2KRAdminPlugin plugin, string msg)
    {
        var prefix = plugin.Config.ChatPrefix;
        Server.PrintToChatAll($"{prefix}{msg}");
    }

    public static string AdminSteamId(CCSPlayerController? caller) =>
        caller == null || !caller.IsValid ? "CONSOLE" : caller.SteamID.ToString();

    public static string AdminName(CCSPlayerController? caller) =>
        caller == null || !caller.IsValid ? "CONSOLE" : (caller.PlayerName ?? "Unknown");

    public static string PayloadJson(object obj) => JsonSerializer.Serialize(obj);

    public static bool RequireFlag(CS2KRAdminPlugin plugin, CCSPlayerController? caller, string flag, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return true; // 콘솔
        if (!plugin.Permissions.HasFlag(caller, flag))
        {
            Reply(info, plugin, "이 명령을 실행할 권한이 없습니다.");
            return false;
        }
        return true;
    }

    public static bool RequireCanTarget(CS2KRAdminPlugin plugin, CCSPlayerController? caller, CCSPlayerController target, CommandInfo info)
    {
        if (caller == null || !caller.IsValid) return true; // 콘솔
        if (!plugin.Permissions.CanTarget(caller, target))
        {
            Reply(info, plugin, $"{target.PlayerName} 은(는) 면역 등급이 더 높습니다.");
            return false;
        }
        return true;
    }
}
