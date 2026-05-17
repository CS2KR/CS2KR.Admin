using CS2KR.Admin.Models;
using MySqlConnector;

namespace CS2KR.Admin.Database;

public sealed class EventRepository
{
    private readonly DbConnectionFactory _factory;

    public EventRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<long> GetMaxIdAsync()
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(id), 0) FROM sa_admin_events";
        var v = await cmd.ExecuteScalarAsync();
        return v == null || v == DBNull.Value ? 0L : Convert.ToInt64(v);
    }

    public async Task<List<AdminEvent>> FetchSinceAsync(long cursor, int? serverId, int limit = 200)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, event_type, target_steamid, target_record_id, payload, server_id, created_at
            FROM sa_admin_events
            WHERE id > @cursor
              AND (server_id IS NULL OR server_id = @srv)
            ORDER BY id ASC
            LIMIT @lim
        """;
        cmd.Parameters.AddWithValue("@cursor", cursor);
        cmd.Parameters.AddWithValue("@srv", (object?)serverId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lim", limit);
        var result = new List<AdminEvent>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            result.Add(new AdminEvent
            {
                Id = r.AsInt64("id"),
                EventType = r.AsString("event_type"),
                TargetSteamId = r.AsStringOrNull("target_steamid"),
                TargetRecordId = r.HasNull("target_record_id") ? null : r.AsInt64("target_record_id"),
                RawPayload = r.AsStringOrNull("payload"),
                ServerId = r.HasNull("server_id") ? null : r.AsInt32("server_id"),
                CreatedAt = r.AsDateTimeOrNull("created_at") ?? DateTime.UtcNow,
            });
        }
        return result;
    }

    public async Task EmitAsync(
        string eventType,
        string? targetSteamId,
        long? targetRecordId,
        string payloadJson,
        int? serverId)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sa_admin_events (event_type, target_steamid, target_record_id, payload, server_id, created_at)
            VALUES (@type, @sid, @rid, @payload, @srv, NOW())
        """;
        cmd.Parameters.AddWithValue("@type", eventType);
        cmd.Parameters.AddWithValue("@sid", (object?)targetSteamId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rid", (object?)targetRecordId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@payload", payloadJson);
        cmd.Parameters.AddWithValue("@srv", (object?)serverId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}
