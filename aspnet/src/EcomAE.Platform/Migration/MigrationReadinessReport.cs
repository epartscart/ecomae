namespace EcomAE.Platform.Migration;

public sealed record MigrationReadinessReport(
    string OverallStatus,
    bool PhpRemovalReady,
    IReadOnlyCollection<MigrationReadinessItem> Items,
    string[] ProductionCutoverGates);
