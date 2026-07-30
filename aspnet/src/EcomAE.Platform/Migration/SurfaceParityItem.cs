namespace EcomAE.Platform.Migration;

public sealed record SurfaceParityItem(
    string Surface,
    string Capability,
    string LegacyRoute,
    string AspNetRoute,
    string Status,
    string RequiredEvidence);
