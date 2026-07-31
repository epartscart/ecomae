namespace EcomAE.Workers;

public interface IMigrationWorkerJobRunner
{
    MigrationWorkerJobRunResult PlanRun(MigrationWorkerJobRunRequest request);
}
