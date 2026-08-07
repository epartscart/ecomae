using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

/// <summary>Part 2 Ch.9 — layered memory model (sensory → strategic → archive).</summary>
public interface ILifeOsMemorySystem
{
    LifeOsMemoryEntry Store(LifeOsMemoryLayer layer, string key, string content, IReadOnlyDictionary<string, string>? tags = null);

    IReadOnlyList<LifeOsMemoryEntry> Retrieve(LifeOsMemoryLayer? layer = null, string? query = null, int take = 20);

    LifeOsMemorySnapshot Snapshot();

    IReadOnlyList<LifeOsMemoryEntry> SeedDemoProject();
}
