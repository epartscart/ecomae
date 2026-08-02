namespace EcomAE.Workers;

public sealed class MigrationWorkerBatchDryRunReporter : IMigrationWorkerBatchDryRunReporter
{
    private readonly MigrationWorkerJobCatalog _catalog;
    private readonly IMigrationWorkerDryRunEvidenceProvider _evidenceProvider;

    public MigrationWorkerBatchDryRunReporter(
        MigrationWorkerJobCatalog catalog,
        IMigrationWorkerDryRunEvidenceProvider evidenceProvider)
    {
        _catalog = catalog;
        _evidenceProvider = evidenceProvider;
    }

    public MigrationWorkerBatchDryRunReport BuildReport(DateTimeOffset requestedAt, string requestedBy)
    {
        var evidenceItems = _catalog.Jobs
            .OrderBy(job => job.Key, StringComparer.Ordinal)
            .Select(job => _evidenceProvider.BuildEvidence(
                job,
                new MigrationWorkerJobRunRequest(job.Key, requestedAt, requestedBy)))
            .ToArray();

        return new MigrationWorkerBatchDryRunReport(
            "batch-1-worker-dry-run-replacements",
            "dry-run-evidence-ready",
            evidenceItems.Length,
            evidenceItems.Length,
            PhpFallbackRequired: true,
            evidenceItems,
            RemainingBlockers:
            [
                "concrete ASP.NET worker implementations are not enabled for writes",
                "database/write parity samples are not attached",
                "production smoke has not run",
                "release-owner cutover approval is missing",
                "PHP schedulers remain authoritative fallback"
            ]);
    }
}
