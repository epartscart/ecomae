using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

/// <summary>Part 2 Ch.8 — builds a scored Context Object from multimodal sources.</summary>
public interface ILifeOsContextEngine
{
    LifeOsContextObject Build(LifeOsEvent trigger, IReadOnlyList<LifeOsMemoryEntry>? memoryHints = null);

    IReadOnlyList<string> KnownSourceNames { get; }
}
