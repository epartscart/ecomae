using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Part3;

/// <summary>Part 3 Ch.14 — transforms raw sensory input into structured knowledge.</summary>
public interface ILifeOsPerceptionEngine
{
    IReadOnlyList<string> SupportedInputs { get; }

    LifeOsPerceptionResult Perceive(LifeOsEvent input);

    object Digest();
}
