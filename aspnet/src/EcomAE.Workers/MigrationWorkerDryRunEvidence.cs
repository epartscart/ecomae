namespace EcomAE.Workers;

public sealed record MigrationWorkerDryRunEvidence(
    string JobKey,
    string LegacyPhpEntry,
    string TargetService,
    string PhpBaselineSample,
    string AspNetDryRunSample,
    string ParityComparison,
    string RollbackCommand,
    string ProductionSmokeStatus,
    bool PhpFallbackRequired,
    IReadOnlyList<string> RequiredApprovals);
