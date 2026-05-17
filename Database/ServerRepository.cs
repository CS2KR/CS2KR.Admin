using MySqlConnector;

namespace CS2KR.Admin.Database;

public sealed class ServerRepository
{
    private readonly DbConnectionFactory _factory;

    public ServerRepository(DbConnectionFactory factory) => _factory = factory;

    /// <summary>
    /// sa_servers 에서 자기 자신의 server_id 찾기. address/hostname 둘 다 검사.
    /// 매치되는 행이 없으면 null 반환 → 플러그인은 글로벌 모드로 동작.
    /// </summary>
    public async Task<(int? id, string? name)> FindServerAsync(string? address, int port)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, hostname FROM sa_servers
            WHERE port = @p AND (address = @a OR hostname = @a)
            ORDER BY id ASC
            LIMIT 1
        """;
        cmd.Parameters.AddWithValue("@p", port);
        cmd.Parameters.AddWithValue("@a", (object?)address ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return (null, null);
        var id = r.HasNull("id") ? (int?)null : r.AsInt32("id");
        var name = r.AsStringOrNull("hostname");
        return (id, name);
    }

    public async Task<string?> GetHostnameByIdAsync(int serverId)
    {
        await using var conn = await _factory.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT hostname FROM sa_servers WHERE id = @id LIMIT 1";
        cmd.Parameters.AddWithValue("@id", serverId);
        var v = await cmd.ExecuteScalarAsync();
        return v == null || v == DBNull.Value ? null : v.ToString();
    }
}
