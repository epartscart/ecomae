using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

/// <summary>Part 2 Ch.11 + Part 3 Ch.18 — goal decomposition and planner types.</summary>
public interface ILifeOsPlanningEngine
{
    LifeOsPlan Decompose(string goal);

    LifeOsPlan SampleLifeOsMvp();

    IReadOnlyList<string> PlannerTypes { get; }

    object PlannerTypesDigest();
}
