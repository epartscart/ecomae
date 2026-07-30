namespace EcomAE.Platform.Migration;

public sealed record SurfaceParityReport(
    string Status,
    IReadOnlyCollection<SurfaceParityItem> Items,
    string[] RequiredBeforeFiftyPercent);
