using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CS2KR.Admin.Models;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

public sealed class PermissionService
{
    private readonly CS2KRAdminPlugin _plugin;
    private ConcurrentDictionary<ulong, CachedAdmin> _bySteamId64 = new();

    public PermissionService(CS2KRAdminPlugin plugin) => _plugin = plugin;

    public async Task ReloadAsync()
    {
        try
        {
            var admins = await _plugin.AdminRepo.LoadAllAsync(_plugin.ServerId);
            var dict = new ConcurrentDictionary<ulong, CachedAdmin>();
            foreach (var a in admins)
            {
                if (ulong.TryParse(a.SteamId, out var sid))
                    dict[sid] = a;
            }
            _bySteamId64 = dict;
            _plugin.Logger.LogInformation("어드민 캐시 리로드 — {Count}명", dict.Count);
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError(e, "어드민 리로드 실패");
        }
    }

    public CachedAdmin? Get(ulong steamId64)
    {
        return _bySteamId64.TryGetValue(steamId64, out var a) ? a : null;
    }

    public CachedAdmin? Get(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return null;
        return Get(player.SteamID);
    }

    public bool HasFlag(CCSPlayerController? player, string flag)
    {
        var a = Get(player);
        return a != null && a.HasFlag(flag);
    }

    public bool CanTarget(CCSPlayerController? admin, CCSPlayerController? target)
    {
        if (admin == null || !admin.IsValid) return false;
        if (target == null || !target.IsValid) return false;

        var adm = Get(admin);
        if (adm == null) return false;
        if (adm.HasFlag("@css/root")) return true;

        var t = Get(target);
        if (t == null) return true;
        return adm.Immunity >= t.Immunity;
    }

    public IEnumerable<CachedAdmin> AllAdmins() => _bySteamId64.Values;
}
