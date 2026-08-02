namespace EcomAE.Workers;

public sealed record MigrationWorkerJobDryRunOutput(
    string JobKey,
    string Status,
    string Summary,
    IReadOnlyDictionary<string, string> Metrics,
    IReadOnlyList<string> Warnings,
    bool WritesBlocked);
