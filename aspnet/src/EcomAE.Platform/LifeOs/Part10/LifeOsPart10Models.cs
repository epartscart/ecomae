namespace EcomAE.Platform.LifeOs.Part10;

public sealed record LifeOsPhase(
    int Number,
    string Title,
    string Horizon,
    IReadOnlyList<string> Deliverables,
    string Outcome);

public sealed record LifeOsStackLayer(string Layer, string RecommendedStack);

public sealed record LifeOsRevenueStream(string Key, string Title);

public sealed record LifeOsRisk(string Key, string Risk, string MitigationTheme);
