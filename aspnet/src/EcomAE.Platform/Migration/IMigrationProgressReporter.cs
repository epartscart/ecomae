namespace EcomAE.Platform.Migration;

public interface IMigrationProgressReporter
{
    MigrationProgressReport BuildReport();
}
