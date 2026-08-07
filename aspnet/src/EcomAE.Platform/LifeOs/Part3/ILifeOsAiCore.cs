using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Part3;

/// <summary>
/// Part 3 Ch.12 — Artificial Intelligence Core (Cognitive Operating System).
/// Runs the unified cognitive cycle (Ch.25).
/// </summary>
public interface ILifeOsAiCore
{
    LifeOsCognitiveCycleResult RunCycle(LifeOsEvent input, bool userPermission = false);

    object ArchitectureDigest();

    object FullPart3Digest();
}
