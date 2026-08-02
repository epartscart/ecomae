namespace EcomAE.Workers;

public interface IMigrationWorkerJobDryRunExecutor
{
    bool CanExecute(MigrationWorkerJob job);

    MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request);
}
