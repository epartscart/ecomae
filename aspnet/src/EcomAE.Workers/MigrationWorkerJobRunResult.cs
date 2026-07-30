namespace EcomAE.Workers;

public sealed record MigrationWorkerJobRunResult(
    string JobKey,
    string Status,
    bool DryRun,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt);
