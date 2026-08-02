namespace EcomAE.Platform.Presentation;

public sealed record PresentationParitySurface(
    string SurfaceKey,
    string AspNetShellRoute,
    string LegacyChromeSource,
    IReadOnlyList<string> Stylesheets,
    string Negotiation,
    string Status);
