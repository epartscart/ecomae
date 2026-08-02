namespace EcomAE.Workers;

public sealed record MigrationWorkerJobRunRequest(
    string JobKey,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    bool DryRun = true,
    IReadOnlyDictionary<string, string>? Parameters = null);
