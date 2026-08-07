using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Part3;

namespace EcomAE.Platform.LifeOs.Engines;

/// <summary>Part 2 Ch.8 + Part 3 Ch.15 — scored Context Object and Current Reality Model (CRM).</summary>
public interface ILifeOsContextEngine
{
    LifeOsContextObject Build(LifeOsEvent trigger, IReadOnlyList<LifeOsMemoryEntry>? memoryHints = null);

    LifeOsCurrentRealityModel BuildCurrentReality(LifeOsEvent trigger, LifeOsContextObject context);

    IReadOnlyList<string> KnownSourceNames { get; }

    object CrmDigest();
}
