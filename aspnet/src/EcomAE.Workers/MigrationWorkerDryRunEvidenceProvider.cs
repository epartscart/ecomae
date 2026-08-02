namespace EcomAE.Workers;

public sealed class MigrationWorkerDryRunEvidenceProvider : IMigrationWorkerDryRunEvidenceProvider
{
    public MigrationWorkerDryRunEvidence BuildEvidence(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var phpBaseline = $"Capture PHP baseline for '{job.LegacyPhpEntry}' before enabling ASP.NET execution; include row counts, generated files, recipients, totals, and audit rows as applicable.";
        var aspNetSample = $"Dry-run only for '{job.TargetService}' requested by '{request.RequestedBy}' at {request.RequestedAt:O}; no business writes or external sends are allowed.";
        var parity = $"Compare ASP.NET dry-run output against PHP baseline for: {job.RequiredParity}";
        var rollback = $"Keep PHP scheduler active; disable ASP.NET worker flag for '{job.Key}' and route execution back to '{job.LegacyPhpEntry}'.";

        return new MigrationWorkerDryRunEvidence(
            job.Key,
            job.LegacyPhpEntry,
            job.TargetService,
            phpBaseline,
            aspNetSample,
            parity,
            rollback,
            "not-run-production-smoke",
            PhpFallbackRequired: true,
            RequiredApprovals:
            [
                "migration-owner",
                "surface-owner",
                "release-owner"
            ]);
    }
}
