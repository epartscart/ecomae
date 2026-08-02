namespace EcomAE.Platform.Migration;

public sealed record ZeroPhpCompletionReport(
    int OverallCompletePercent,
    int OverallPendingPercent,
    string Status,
    IReadOnlyCollection<ZeroPhpCompletionArea> Areas,
    IReadOnlyCollection<string> NextActions);
