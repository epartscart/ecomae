namespace EcomAE.Platform.LifeOs.Part3;

/// <summary>Part 3 Ch.24 — post-task self-evaluation feeding the Learning Engine.</summary>
public interface ILifeOsSelfReflectionEngine
{
    LifeOsReflectionReport Reflect(
        string goal,
        string outcome,
        bool ethicsAllowed,
        double decisionScore);

    object Digest();
}
