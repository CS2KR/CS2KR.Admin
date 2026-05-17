using CS2KR.Admin.Models;
using MySqlConnector;

namespace CS2KR.Admin.Database;

/// <summary>
/// sa_admins + sa_admins_flags + sa_groups + sa_groups_flags 로부터
/// 어드민 권한을 로드. CLAUDE.md §114-126 의 함정을 그대로 구현:
///   - sa_admins_flags 가 권한 소스 (sa_admins.group_id 단독 불충분)
///   - flag '#GroupName' 은 sa_groups_flags 로 확장
///   - flag '@css/...' 는 직접 권한
/// </summary>
public sealed class AdminRepository
{
    private readonly DbConnectionFactory _factory;

    public AdminRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<List<CachedAdmin>> LoadAllAsync(int? serverId)
    {
        await using var conn = await _factory.OpenAsync();

        // 1) 어드민 목록 (만료 안 됨)
        var admins = new Dictionary<long, AdminRow>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT a.id, a.player_steamid, a.player_name, a.immunity, a.group_id, a.ends, g.name AS group_name
                FROM sa_admins a
                LEFT JOIN sa_groups g ON g.id = a.group_id
                WHERE (a.server_id IS NULL OR a.server_id = @sid)
                  AND (a.ends IS NULL OR a.ends > NOW())
            """;
            cmd.Parameters.AddWithValue("@sid", (object?)serverId ?? DBNull.Value);

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = r.AsInt64("id");
                admins[id] = new AdminRow
                {
                    Id = id,
                    SteamId = r.AsString("player_steamid"),
                    Name = r.AsStringOrNull("player_name") ?? "",
                    Immunity = r.AsInt32("immunity"),
                    GroupId = r.HasNull("group_id") ? null : r.AsInt32("group_id"),
                    GroupName = r.AsStringOrNull("group_name"),
                    Ends = r.AsDateTimeOrNull("ends"),
                };
            }
        }

        if (admins.Count == 0) return new List<CachedAdmin>();

        // 2) sa_admins_flags (직접 플래그 + #그룹 참조)
        var adminFlagRefs = new Dictionary<long, List<string>>();
        await using (var cmd = conn.CreateCommand())
        {
            var idList = string.Join(",", admins.Keys);
            cmd.CommandText = $"SELECT admin_id, flag FROM sa_admins_flags WHERE admin_id IN ({idList})";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var aid = r.AsInt64("admin_id");
                var flag = r.AsString("flag");
                if (!adminFlagRefs.TryGetValue(aid, out var list))
                    adminFlagRefs[aid] = list = new List<string>();
                list.Add(flag);
            }
        }

        // 3) sa_groups_flags (#그룹 참조 시 확장용)
        var groupFlags = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT g.name AS group_name, gf.flag
                FROM sa_groups g
                INNER JOIN sa_groups_flags gf ON gf.group_id = g.id
            """;
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var name = r.AsString("group_name");
                var flag = r.AsString("flag");
                if (!groupFlags.TryGetValue(name, out var set))
                    groupFlags[name] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(flag);
            }
        }

        // 4) 합성
        var result = new List<CachedAdmin>(admins.Count);
        foreach (var (id, row) in admins)
        {
            var flagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (adminFlagRefs.TryGetValue(id, out var refs))
            {
                foreach (var f in refs)
                {
                    if (f.StartsWith("#"))
                    {
                        // 그룹 참조 확장
                        var gname = f.Substring(1);
                        if (groupFlags.TryGetValue(gname, out var gset))
                            foreach (var gf in gset) flagSet.Add(gf);
                    }
                    else
                    {
                        flagSet.Add(f);
                    }
                }
            }

            result.Add(new CachedAdmin
            {
                Id = id,
                SteamId = row.SteamId,
                Name = row.Name,
                Immunity = row.Immunity,
                GroupId = row.GroupId,
                GroupName = row.GroupName,
                Flags = flagSet,
                Ends = row.Ends,
            });
        }
        return result;
    }

    private sealed class AdminRow
    {
        public long Id;
        public string SteamId = "";
        public string Name = "";
        public int Immunity;
        public int? GroupId;
        public string? GroupName;
        public DateTime? Ends;
    }
}
