using MySqlConnector;

namespace CS2KR.Admin.Database;

/// <summary>
/// MySqlConnector 2.3.7 부터 MySqlDataReader 가 자체 GetInt32(string)/GetString(string)/GetDateTime(string)
/// 인스턴스 메서드를 갖는다 — 인스턴스 메서드는 확장 메서드를 shadowing 하므로 동일 이름으로 만들면
/// 우리 코드가 호출되지 않는다. 따라서 모든 확장 메서드는 다른 이름(Has* / As*)을 사용한다.
///
/// 핵심: CS2-SimpleAdmin 의 sa_admins.immunity 가 VARCHAR(255) 로 저장되어 있어
/// GetInt32(int) 의 직접 캐스트가 InvalidCastException 을 던진다 — 그래서 AsInt32 가 필요.
/// </summary>
internal static class ReaderExtensions
{
    public static bool HasNull(this MySqlDataReader r, string name) => r.IsDBNull(r.GetOrdinal(name));

    /// <summary>VARCHAR/INT/BIGINT 어떤 타입이든 안전하게 int 로 변환.</summary>
    public static int AsInt32(this MySqlDataReader r, string name)
    {
        var ord = r.GetOrdinal(name);
        if (r.IsDBNull(ord)) return 0;
        var v = r.GetValue(ord);
        return v switch
        {
            int i => i,
            long l => checked((int)l),
            uint ui => checked((int)ui),
            ulong ul => checked((int)ul),
            short s => s,
            byte b => b,
            sbyte sb => sb,
            string str => int.TryParse(str, out var p) ? p : 0,
            _ => Convert.ToInt32(v),
        };
    }

    public static long AsInt64(this MySqlDataReader r, string name)
    {
        var ord = r.GetOrdinal(name);
        if (r.IsDBNull(ord)) return 0;
        var v = r.GetValue(ord);
        return v switch
        {
            long l => l,
            int i => i,
            uint ui => ui,
            ulong ul => checked((long)ul),
            short s => s,
            byte b => b,
            string str => long.TryParse(str, out var p) ? p : 0,
            _ => Convert.ToInt64(v),
        };
    }

    public static string AsString(this MySqlDataReader r, string name)
    {
        var ord = r.GetOrdinal(name);
        return r.IsDBNull(ord) ? "" : r.GetValue(ord).ToString() ?? "";
    }

    public static string? AsStringOrNull(this MySqlDataReader r, string name)
    {
        var ord = r.GetOrdinal(name);
        return r.IsDBNull(ord) ? null : r.GetValue(ord).ToString();
    }

    public static DateTime? AsDateTimeOrNull(this MySqlDataReader r, string name)
    {
        var ord = r.GetOrdinal(name);
        return r.IsDBNull(ord) ? null : r.GetDateTime(ord);
    }
}
