namespace EcomAE.Workers;

public sealed record MigrationWorkerJob(
    string Key,
    string LegacyPhpEntry,
    string TargetService,
    string Schedule,
    string Status,
    string RequiredParity);
