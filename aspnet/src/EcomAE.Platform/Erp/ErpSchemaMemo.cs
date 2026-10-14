using System.Collections.Concurrent;
using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Remembers schema work that already succeeded for a database in this process.
/// The first request still creates and seeds. Later requests skip the same DDL.
/// </summary>
public static class ErpSchemaMemo
{
    private static readonly ConcurrentDictionary<string, byte> Ready = new(StringComparer.Ordinal);

    public static string DatabaseKey(string? dataSource, string? database, string token)
    {
        if (string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return (dataSource ?? string.Empty).Trim() + "/" + database.Trim() + "|" + token;
    }

    public static string Key(DbConnection connection, string token)
        => DatabaseKey(connection.DataSource, connection.Database, token);

    public static bool IsReady(string key)
        => key.Length > 0 && Ready.ContainsKey(key);

    public static void Remember(string key)
    {
        if (key.Length > 0)
        {
            Ready.TryAdd(key, 0);
        }
    }
}
