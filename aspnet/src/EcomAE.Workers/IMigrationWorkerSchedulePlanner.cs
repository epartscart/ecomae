namespace EcomAE.Workers;

public interface IMigrationWorkerSchedulePlanner
{
    MigrationWorkerJobSchedulePlan BuildPlan(string jobKey);
}
