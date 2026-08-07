using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

/// <summary>Part 2 Ch.11 — converts high-level goals into executable workflows.</summary>
public interface ILifeOsPlanningEngine
{
    LifeOsPlan Decompose(string goal);

    LifeOsPlan SampleLifeOsMvp();
}
