using System.Collections.Concurrent;

namespace EcomAE.Platform.Presentation;

public sealed record ErpExternalReportingPack(
    ErpExternalReportingBuilt Built,
    string Kind,
    string Notice,
    DateTime StoredUtc);

public interface IErpExternalReportingPackCache
{
    string Store(ErpExternalReportingBuilt built, string kind, string notice);
    bool TryGet(string? id, out ErpExternalReportingPack? pack);
}

public sealed class ErpExternalReportingPackCache : IErpExternalReportingPackCache
{
    private readonly ConcurrentDictionary<string, ErpExternalReportingPack> _packs = new(StringComparer.Ordinal);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public string Store(ErpExternalReportingBuilt built, string kind, string notice)
    {
        var id = Guid.NewGuid().ToString("N");
        _packs[id] = new(built, kind, notice ?? "", DateTime.UtcNow);
        Prune();
        return id;
    }

    public bool TryGet(string? id, out ErpExternalReportingPack? pack)
    {
        pack = null;
        if (string.IsNullOrWhiteSpace(id)) return false;
        if (!_packs.TryGetValue(id, out var found)) return false;
        if (DateTime.UtcNow - found.StoredUtc > Ttl)
        {
            _packs.TryRemove(id, out _);
            return false;
        }

        pack = found;
        return true;
    }

    private void Prune()
    {
        if (_packs.Count < 64) return;
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var kv in _packs)
        {
            if (kv.Value.StoredUtc < cutoff) _packs.TryRemove(kv.Key, out _);
        }
    }
}
