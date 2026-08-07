namespace EcomAE.Platform.LifeOs.Part3;

/// <summary>Part 3 Ch.19 — predicts future events from history + CRM.</summary>
public interface ILifeOsPredictionEngine
{
    IReadOnlyList<LifeOsPrediction> Predict(LifeOsCurrentRealityModel reality, string intent);

    object Digest();
}
