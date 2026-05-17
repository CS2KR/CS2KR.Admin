using CS2KR.Admin.Models;
using MySqlConnector;

namespace CS2KR.Admin.Database;

public sealed class MuteRepository
{
    private readonly DbConnectionFactory _factory;

    public MuteRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<List<CachedMute>> GetActiveBySteamIdAsync(string steamId, int? serverId)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_steamid, player_name, reason, duration, ends, type, status, server_id
            FROM sa_mutes
            WHERE player_steamid = @sid AND status = 'ACTIVE'
              AND (server_id IS NULL OR server_id = @srv)
              AND (duration = 0 OR ends > NOW())
        """;
        cmd.Parameters.AddWithValue("@sid", steamId);
        cmd.Parameters.AddWithValue("@srv", (object?)serverId ?? DBNull.Value);
        var result = new List<CachedMute>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(Read(r));
        return result;
    }

    public async Task<CachedMute?> GetActiveByIdAsync(long id)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, player_steamid, player_name, reason, duration, ends, type, status, server_id
            FROM sa_mutes
            WHERE id = @id AND status = 'ACTIVE'
              AND (duration = 0 OR ends > NOW())
            LIMIT 1
        """;
        cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Read(r) : null;
    }

    public async Task<long> InsertAsync(
        string playerSteamId, string? playerName, string reason, int durationMinutes,
        string type, string adminSteamId, string adminName, int? serverId)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sa_mutes (player_name, player_steamid, admin_steamid, admin_name,
                                  reason, duration, ends, created, server_id, type, status)
            VALUES (@name, @sid, @asid, @aname, @reason, @dur,
                    IF(@dur = 0, DATE_ADD(NOW(), INTERVAL 100 YEAR), DATE_ADD(NOW(), INTERVAL @dur MINUTE)),
                    NOW(), @srv, @type, 'ACTIVE');
            SELECT LAST_INSERT_ID();
        """;
        cmd.Parameters.AddWithValue("@name", (object?)playerName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sid", playerSteamId);
        cmd.Parameters.AddWithValue("@asid", adminSteamId);
        cmd.Parameters.AddWithValue("@aname", adminName);
        cmd.Parameters.AddWithValue("@reason", reason);
        cmd.Parameters.AddWithValue("@dur", durationMinutes);
        cmd.Parameters.AddWithValue("@srv", (object?)serverId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@type", type);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    public async Task UnmuteBySteamIdAsync(string playerSteamId, string adminSteamId, string reason, string? type = null)
    {
        await using var conn = await _factory.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var ids = new List<long>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = type == null
                    ? "SELECT id FROM sa_mutes WHERE player_steamid = @sid AND status = 'ACTIVE'"
                    : "SELECT id FROM sa_mutes WHERE player_steamid = @sid AND status = 'ACTIVE' AND type = @t";
                cmd.Parameters.AddWithValue("@sid", playerSteamId);
                if (type != null) cmd.Parameters.AddWithValue("@t", type);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) ids.Add(r.GetInt64(0));
            }

            foreach (var id in ids)
            {
                long unmuteId;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = """
                        INSERT INTO sa_unmutes (mute_id, admin_id, reason, date) VALUES (@m, 0, @r, NOW());
                        SELECT LAST_INSERT_ID();
                    """;
                    cmd.Parameters.AddWithValue("@m", id);
                    cmd.Parameters.AddWithValue("@r", reason);
                    unmuteId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                }
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE sa_mutes SET status='UNMUTED', unmute_id=@u WHERE id=@m";
                    cmd.Parameters.AddWithValue("@u", unmuteId);
                    cmd.Parameters.AddWithValue("@m", id);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static CachedMute Read(MySqlDataReader r) => new()
    {
        Id = r.AsInt64("id"),
        PlayerSteamId = r.AsString("player_steamid"),
        PlayerName = r.AsStringOrNull("player_name"),
        Reason = r.AsString("reason"),
        DurationMinutes = r.AsInt32("duration"),
        Ends = r.AsDateTimeOrNull("ends"),
        Type = r.AsString("type"),
        Status = r.AsString("status"),
        ServerId = r.HasNull("server_id") ? null : r.AsInt32("server_id"),
    };
}
