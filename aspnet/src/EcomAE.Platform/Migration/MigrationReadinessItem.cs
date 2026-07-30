namespace EcomAE.Platform.Migration;

public sealed record MigrationReadinessItem(
    string Surface,
    string LegacyPhpEntry,
    string AspNetDestination,
    string CurrentStatus,
    bool BlocksPhpRemoval,
    string CorrectiveAction);
