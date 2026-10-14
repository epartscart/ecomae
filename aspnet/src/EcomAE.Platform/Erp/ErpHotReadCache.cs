using System.Collections.Concurrent;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Short process-local cache for digests that every ERP page repeats
/// (companies, home, favourites schema). Entries expire; they are not a
/// substitute for a write. Callers skip it when there is no HTTP request so
/// unit tests keep reading the database.
/// </summary>
public static class ErpHotReadCache
{
    private static readonly ConcurrentDictionary<string, Entry> Entries = new(StringComparer.Ordinal);

    public static bool TryGet<T>(string key, out T? value)
    {
        if (key.Length > 0
            && Entries.TryGetValue(key, out var entry)
            && entry.ExpiresAt > Environment.TickCount64
            && entry.Value is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public static void Set<T>(string key, T value, TimeSpan ttl)
    {
        if (key.Length == 0)
        {
            return;
        }

        Entries[key] = new Entry(value, Environment.TickCount64 + (long)ttl.TotalMilliseconds);
        if (Entries.Count > 400)
        {
            var now = Environment.TickCount64;
            foreach (var pair in Entries)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    Entries.TryRemove(pair.Key, out _);
                }
            }
        }
    }

    private sealed record Entry(object? Value, long ExpiresAt);
}
