namespace EcomAE.Workers;

public sealed record MigrationWorkerBatchDryRunReport(
    string BatchKey,
    string Status,
    int TotalJobs,
    int DryRunEvidenceReadyJobs,
    bool PhpFallbackRequired,
    IReadOnlyList<MigrationWorkerDryRunEvidence> EvidenceItems,
    IReadOnlyList<string> RemainingBlockers);
