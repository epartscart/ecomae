namespace EcomAE.Workers;

public interface IMigrationWorkerDryRunEvidenceProvider
{
    MigrationWorkerDryRunEvidence BuildEvidence(MigrationWorkerJob job, MigrationWorkerJobRunRequest request);
}
