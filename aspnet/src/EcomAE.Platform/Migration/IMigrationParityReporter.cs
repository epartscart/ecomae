namespace EcomAE.Platform.Migration;

public interface IMigrationParityReporter
{
    MigrationParityReport BuildReport();
}
