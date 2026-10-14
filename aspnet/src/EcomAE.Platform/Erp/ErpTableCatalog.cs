using System.Collections.Concurrent;
using System.Data.Common;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

/// <summary>
/// <c>SHOW TABLES</c> once per database, then reuse the names. A missing-table
/// probe through <c>information_schema</c> is hundreds of milliseconds on a
/// large tenant; <c>SHOW TABLES</c> is one short round trip.
/// </summary>
public static class ErpTableCatalog
{
    private static readonly ConcurrentDictionary<string, Snapshot> Cache = new(StringComparer.Ordinal);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public static async Task<IReadOnlySet<string>> LoadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var key = ErpSchemaMemo.Key(connection, "tables");
        if (key.Length > 0
            && Cache.TryGetValue(key, out var hit)
            && hit.ExpiresAt > Environment.TickCount64)
        {
            return hit.Names;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        ErpFirstPaint.ApplyIfErp(command);
        command.CommandText = "SHOW TABLES";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
            {
                names.Add(reader.GetString(0));
            }
        }

        if (key.Length > 0)
        {
            Cache[key] = new Snapshot(names, Environment.TickCount64 + (long)Ttl.TotalMilliseconds);
        }

        return names;
    }

    public static void Forget(DbConnection connection)
    {
        var key = ErpSchemaMemo.Key(connection, "tables");
        if (key.Length > 0)
        {
            Cache.TryRemove(key, out _);
        }
    }

    private sealed record Snapshot(HashSet<string> Names, long ExpiresAt);
}
