namespace EcomAE.Workers;

public interface IMigrationWorkerBatchDryRunReporter
{
    MigrationWorkerBatchDryRunReport BuildReport(DateTimeOffset requestedAt, string requestedBy);
}
