namespace EcomAE.Platform.Migration;

public interface IMigrationReadinessReporter
{
    MigrationReadinessReport BuildReport();
}
