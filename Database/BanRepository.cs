using CS2KR.Admin.Models;
using MySqlConnector;

namespace CS2KR.Admin.Database;

public sealed class BanRepository
{
    private readonly DbConnectionFactory _factory;

    public BanRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<CachedBan?> GetActiveByIdAsync(long id)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_steamid, player_name, player_ip, reason, duration, ends, status, server_id
            FROM sa_bans
            WHERE id = @id AND status = 'ACTIVE'
              AND (duration = 0 OR ends > NOW())
            LIMIT 1
        """;
        cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Read(r) : null;
    }

    public async Task<CachedBan?> GetActiveBySteamIdAsync(string steamId, int? serverId)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_steamid, player_name, player_ip, reason, duration, ends, status, server_id
            FROM sa_bans
            WHERE player_steamid = @sid AND status = 'ACTIVE'
              AND (server_id IS NULL OR server_id = @srv)
              AND (duration = 0 OR ends > NOW())
            ORDER BY created DESC
            LIMIT 1
        """;
        cmd.Parameters.AddWithValue("@sid", steamId);
        cmd.Parameters.AddWithValue("@srv", (object?)serverId ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Read(r) : null;
    }

    public async Task<long> InsertAsync(
        string playerSteamId,
        string? playerName,
        string? playerIp,
        string reason,
        int durationMinutes,
        string adminSteamId,
        string adminName,
        int? serverId)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sa_bans (player_name, player_steamid, player_ip, admin_steamid, admin_name,
                                 reason, duration, ends, created, server_id, status)
            VALUES (@name, @sid, @ip, @asid, @aname, @reason, @dur,
                    IF(@dur = 0, DATE_ADD(NOW(), INTERVAL 100 YEAR), DATE_ADD(NOW(), INTERVAL @dur MINUTE)),
                    NOW(), @srv, 'ACTIVE');
            SELECT LAST_INSERT_ID();
        """;
        cmd.Parameters.AddWithValue("@name", (object?)playerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sid", playerSteamId);
        cmd.Parameters.AddWithValue("@ip", (object?)playerIp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@asid", adminSteamId);
        cmd.Parameters.AddWithValue("@aname", adminName);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@dur", durationMinutes);
        cmd.Parameters.AddWithValue("@srv", (object?)serverId ?? DBNull.Value);
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    public async Task UnbanBySteamIdAsync(string playerSteamId, string adminSteamId, string reason)
    {
        await using var conn = await _factory.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // 활성 밴 한 건 가져오기
            long? banId = null;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT id FROM sa_bans WHERE player_steamid = @sid AND status = 'ACTIVE' ORDER BY id DESC LIMIT 1";
                cmd.Parameters.AddWithValue("@sid", playerSteamId);
                var v = await cmd.ExecuteScalarAsync();
                if (v != null && v != DBNull.Value) banId = Convert.ToInt64(v);
            }

            if (banId == null) { await tx.CommitAsync(); return; }

            long unbanId;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO sa_unbans (ban_id, admin_id, reason, date) VALUES (@b, 0, @r, NOW());
                    SELECT LAST_INSERT_ID();
                """;
                cmd.Parameters.AddWithValue("@b", banId.Value);
                cmd.Parameters.AddWithValue("@r", reason);
                unbanId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE sa_bans SET status='UNBANNED', unban_id=@u WHERE id=@b";
                cmd.Parameters.AddWithValue("@u", unbanId);
                cmd.Parameters.AddWithValue("@b", banId.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static CachedBan Read(MySqlDataReader r) => new()
    {
        Id = r.AsInt64("id"),
        PlayerSteamId = r.AsString("player_steamid"),
        PlayerName = r.AsStringOrNull("player_name"),
        PlayerIp = r.AsStringOrNull("player_ip"),
        Reason = r.AsString("reason"),
        DurationMinutes = r.AsInt32("duration"),
        Ends = r.AsDateTimeOrNull("ends"),
        Status = r.AsString("status"),
        ServerId = r.HasNull("server_id") ? null : r.AsInt32("server_id"),
    };
}
