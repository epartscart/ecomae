namespace EcomAE.Platform.Migration;

public sealed record MigrationProgressReport(
    int OverallCompletePercent,
    int OverallPendingPercent,
    string Summary,
    IReadOnlyCollection<MigrationProgressItem> Items);
