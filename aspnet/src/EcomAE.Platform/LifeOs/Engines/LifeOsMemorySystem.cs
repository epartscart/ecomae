using System.Collections.Concurrent;
using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

public sealed class LifeOsMemorySystem : ILifeOsMemorySystem
{
    private readonly ConcurrentBag<LifeOsMemoryEntry> _entries = [];
    private int _seq;

    private static readonly IReadOnlyDictionary<LifeOsMemoryLayer, TimeSpan?> Retention = new Dictionary<LifeOsMemoryLayer, TimeSpan?>
    {
        [LifeOsMemoryLayer.Sensory] = TimeSpan.FromSeconds(30),
        [LifeOsMemoryLayer.Working] = TimeSpan.FromMinutes(30),
        [LifeOsMemoryLayer.Conversation] = null, // active session
        [LifeOsMemoryLayer.Task] = TimeSpan.FromDays(14),
        [LifeOsMemoryLayer.Project] = null,
        [LifeOsMemoryLayer.Personal] = null,
        [LifeOsMemoryLayer.Knowledge] = null,
        [LifeOsMemoryLayer.Experience] = null,
        [LifeOsMemoryLayer.Strategic] = null,
        [LifeOsMemoryLayer.Archive] = null,
    };

    public LifeOsMemoryEntry Store(
        LifeOsMemoryLayer layer,
        string key,
        string content,
        IReadOnlyDictionary<string, string>? tags = null)
    {
        var id = $"MEM-{Interlocked.Increment(ref _seq):D6}";
        Retention.TryGetValue(layer, out var retention);
        var entry = new LifeOsMemoryEntry(id, layer, key, content, DateTimeOffset.UtcNow, retention, tags);
        _entries.Add(entry);
        return entry;
    }

    public IReadOnlyList<LifeOsMemoryEntry> Retrieve(LifeOsMemoryLayer? layer = null, string? query = null, int take = 20)
    {
        IEnumerable<LifeOsMemoryEntry> q = _entries;
        if (layer is not null)
        {
            q = q.Where(e => e.Layer == layer);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(e =>
                e.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Content.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return q.OrderByDescending(e => e.StoredAt).Take(Math.Clamp(take, 1, 100)).ToList();
    }

    public LifeOsMemorySnapshot Snapshot()
    {
        var list = _entries.ToArray();
        var counts = Enum.GetValues<LifeOsMemoryLayer>()
            .ToDictionary(l => l.ToString(), l => list.Count(e => e.Layer == l), StringComparer.Ordinal);
        return new LifeOsMemorySnapshot(
            DateTimeOffset.UtcNow,
            counts,
            list.OrderByDescending(e => e.StoredAt).Take(12).ToList());
    }

    public IReadOnlyList<LifeOsMemoryEntry> SeedDemoProject()
    {
        if (_entries.Any(e => e.Layer == LifeOsMemoryLayer.Project))
        {
            return Retrieve(LifeOsMemoryLayer.Project, take: 20);
        }

        var seeded = new List<LifeOsMemoryEntry>
        {
            Store(LifeOsMemoryLayer.Strategic, "goal:lifeos", "Build LifeOS — Universal Ambient AI OS"),
            Store(LifeOsMemoryLayer.Strategic, "goal:bos-v3", "Launch BOS v3"),
            Store(LifeOsMemoryLayer.Project, "lifeos:architecture", "Part 1 vision + Part 2 orchestrator/event-bus/memory/agents/planning"),
            Store(LifeOsMemoryLayer.Project, "lifeos:apis", "Scaffold digests under /lifeos/* — runtime agents later"),
            Store(LifeOsMemoryLayer.Project, "lifeos:design", "Privacy by design; explainability; human control"),
            Store(LifeOsMemoryLayer.Knowledge, "engines", "Nine brain engines + multi-agent framework"),
            Store(LifeOsMemoryLayer.Working, "cycle", "Current reasoning cycle: Part 2 scaffold"),
            Store(LifeOsMemoryLayer.Conversation, "session", "Operator exploring LifeOS console"),
        };
        return seeded;
    }
}
