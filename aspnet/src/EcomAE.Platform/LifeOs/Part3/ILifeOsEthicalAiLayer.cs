namespace EcomAE.Platform.LifeOs.Part3;

/// <summary>Part 3 Ch.23 — every AI decision passes ethical validation before execution.</summary>
public interface ILifeOsEthicalAiLayer
{
    LifeOsEthicalVerdict Validate(
        string action,
        double confidence,
        bool userPermission,
        bool irreversible);

    object Digest();
}
