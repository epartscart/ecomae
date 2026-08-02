namespace EcomAE.Platform.Migration;

public sealed record DataParityReport(
    string Status,
    IReadOnlyCollection<string> ReadyContracts,
    IReadOnlyCollection<string> ProductionDataSources,
    IReadOnlyCollection<string> RequiredBeforeCutover);
