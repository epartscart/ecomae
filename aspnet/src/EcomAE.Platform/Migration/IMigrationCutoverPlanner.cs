namespace EcomAE.Platform.Migration;

public interface IMigrationCutoverPlanner
{
    MigrationCutoverPlan BuildPlan();
}
