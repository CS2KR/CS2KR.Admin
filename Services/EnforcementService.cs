using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace CS2KR.Admin.Services;

/// <summary>
/// 라이브 플레이어에 대한 ban/mute/kick 적용. 모든 게임 객체 접근은 메인스레드에서 수행.
/// </summary>
public sealed class EnforcementService
{
    private readonly CS2KRAdminPlugin _plugin;
    private readonly ConcurrentDictionary<ulong, byte> _gagged = new();        // GAG | SILENCE
    private readonly ConcurrentDictionary<ulong, byte> _voiceMuted = new();    // MUTE | SILENCE

    public EnforcementService(CS2KRAdminPlugin plugin) => _plugin = plugin;

    public bool IsGagged(ulong steamId) => _gagged.ContainsKey(steamId);
    public bool IsVoiceMuted(ulong steamId) => _voiceMuted.ContainsKey(steamId);

    public void HandleChatAttempt(ulong steamId)
    {
        // 클라이언트 채팅 입력 시 호출. 실제 차단은 OnClientChat 의 hook 으로 처리하면 좋지만
        // CSS# 의 OnClientChat 은 메시지 자체를 막을 수 없음 — 대신 EventPlayerChat 의 HookResult.Stop 사용.
    }

    public void OnPlayerDisconnect(ulong steamId)
    {
        _gagged.TryRemove(steamId, out _);
        _voiceMuted.TryRemove(steamId, out _);
    }

    public async Task OnPlayerJoinAsync(ulong steamId64)
    {
        try
        {
            // 1) 활성 밴 확인 → 즉시 킥
            var ban = await _plugin.BanRepo.GetActiveBySteamIdAsync(steamId64.ToString(), _plugin.ServerId);
            if (ban != null)
            {
                var reason = ban.Reason;
                Server.NextFrame(() => KickIfPresent(steamId64, reason));
                return;
            }

            // 2) 활성 뮤트 확인 → 플래그 설정
            var mutes = await _plugin.MuteRepo.GetActiveBySteamIdAsync(steamId64.ToString(), _plugin.ServerId);
            foreach (var m in mutes) ApplyMute(steamId64, m.Type);
        }
        catch (Exception e)
        {
            _plugin.Logger.LogError(e, "OnPlayerJoinAsync 실패 steamid={S}", steamId64);
        }
    }

    /// <summary>
    /// 호출자가 메인스레드에서 SteamID 리스트를 수집한 뒤 백그라운드에서 enforce.
    /// (Utilities.GetPlayers 는 메인스레드 전용)
    /// </summary>
    public async Task ApplyToOnlineAsync(IEnumerable<ulong> steamIds)
    {
        foreach (var sid in steamIds)
        {
            try { await OnPlayerJoinAsync(sid); }
            catch (Exception e) { _plugin.Logger.LogWarning(e, "ApplyToOnline steamid={S} 실패", sid); }
        }
    }

    public void KickIfPresent(ulong steamId64, string reason)
    {
        var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && !p.IsBot && p.SteamID == steamId64);
        if (player == null) return;
        var msg = string.Format(_plugin.Config.KickMessageBanned, reason);
        Server.ExecuteCommand($"kickid {player.UserId} \"{Sanitize(msg)}\"");
    }

    public void ApplyMute(ulong steamId64, string type)
    {
        switch (type)
        {
            case "GAG":
                _gagged[steamId64] = 1;
                break;
            case "MUTE":
                _voiceMuted[steamId64] = 1;
                ApplyVoiceMuteOnMainThread(steamId64);
                break;
            case "SILENCE":
                _gagged[steamId64] = 1;
                _voiceMuted[steamId64] = 1;
                ApplyVoiceMuteOnMainThread(steamId64);
                break;
        }
    }

    public void RemoveMute(ulong steamId64, string? type = null)
    {
        if (type == null || type == "GAG" || type == "SILENCE")
            _gagged.TryRemove(steamId64, out _);
        if (type == null || type == "MUTE" || type == "SILENCE")
        {
            _voiceMuted.TryRemove(steamId64, out _);
            RemoveVoiceMuteOnMainThread(steamId64);
        }
    }

    private void ApplyVoiceMuteOnMainThread(ulong steamId64)
    {
        Server.NextFrame(() =>
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId64);
            if (player == null || !player.IsValid) return;
            try
            {
                player.VoiceFlags |= VoiceFlags.Muted;
            }
            catch (Exception e)
            {
                _plugin.Logger.LogWarning(e, "음성 뮤트 적용 실패 steamid={S}", steamId64);
            }
        });
    }

    private void RemoveVoiceMuteOnMainThread(ulong steamId64)
    {
        Server.NextFrame(() =>
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == steamId64);
            if (player == null || !player.IsValid) return;
            try
            {
                player.VoiceFlags &= ~VoiceFlags.Muted;
            }
            catch { /* 무시 */ }
        });
    }

    private static string Sanitize(string s) => s.Replace("\"", "'").Replace("\n", " ");
}
