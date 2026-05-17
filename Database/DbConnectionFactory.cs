using MySqlConnector;

namespace CS2KR.Admin.Database;

public sealed class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(DatabaseConfig config)
    {
        _connectionString = config.ConnectionString;
    }

    public async Task<MySqlConnection> OpenAsync()
    {
        var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }
}
