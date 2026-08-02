namespace EcomAE.Platform.Presentation;

public sealed record PresentationParityReport(
    string Status,
    string Contract,
    IReadOnlyCollection<PresentationParitySurface> Surfaces,
    IReadOnlyCollection<string> Guarantees,
    IReadOnlyCollection<string> RemainingGaps);
