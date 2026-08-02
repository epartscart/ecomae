namespace EcomAE.Platform.Migration;

public sealed record ZeroPhpCompletionArea(
    string Name,
    int WeightPercent,
    int CompletePercent,
    string Status,
    IReadOnlyCollection<string> PendingWork);
