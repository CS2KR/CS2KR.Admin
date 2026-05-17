using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

public sealed class ServerIdentityService
{
    private readonly CS2KRAdminPlugin _plugin;

    public ServerIdentityService(CS2KRAdminPlugin plugin) => _plugin = plugin;

    /// <summary>메인스레드 전용 — ConVar 접근 안전성을 위해 분리.</summary>
    public (string? address, int port) ReadBindFromConVars()
    {
        int port = 27015;
        string? address = null;

        try
        {
            var portCvar = ConVar.Find("hostport");
            if (portCvar != null) port = portCvar.GetPrimitiveValue<int>();
        }
        catch { /* 기본값 사용 */ }

        try
        {
            var ipCvar = ConVar.Find("ip");
            if (ipCvar != null)
            {
                var raw = ipCvar.StringValue;
                if (!string.IsNullOrWhiteSpace(raw) && raw != "0.0.0.0") address = raw;
            }
        }
        catch { /* 무시 */ }

        return (address, port);
    }

    public async Task ResolveAsync(string? address, int port)
    {
        if (_plugin.Config.ServerIdOverride > 0)
        {
            _plugin.ServerId = _plugin.Config.ServerIdOverride;
            _plugin.Logger.LogInformation("server_id={Id} (수동 override)", _plugin.ServerId);
            return;
        }

        try
        {
            var sid = await _plugin.ServerRepo.FindServerIdAsync(address, port);
            _plugin.ServerId = sid;
            if (sid.HasValue)
                _plugin.Logger.LogInformation("server_id={Id} (sa_servers 자동 매칭, address={A}, port={P})", sid, address ?? "?", port);
            else
                _plugin.Logger.LogWarning("sa_servers 매칭 실패 (address={A}, port={P}) — 글로벌 NULL 모드로 동작", address ?? "?", port);
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError(e, "서버 식별 실패 — 글로벌 NULL 모드로 동작");
            _plugin.ServerId = null;
        }
    }
}
