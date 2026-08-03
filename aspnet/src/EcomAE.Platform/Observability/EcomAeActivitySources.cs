using System.Diagnostics;

namespace EcomAE.Platform.Observability;

/// <summary>
/// Stable ActivitySource names for future OpenTelemetry wiring.
/// OTLP exporters are not registered yet; this is scaffolding only per Enterprise BOS architecture.
/// </summary>
public static class EcomAeActivitySources
{
    public const string PlatformName = "EcomAE.Platform";
    public const string WorkersName = "EcomAE.Workers";
    public const string AuthName = "EcomAE.Platform.Auth";
    public const string SurfacesName = "EcomAE.Platform.Surfaces";
    public const string DataName = "EcomAE.Platform.Data";

    public static readonly ActivitySource Platform = new(PlatformName);
    public static readonly ActivitySource Auth = new(AuthName);
    public static readonly ActivitySource Surfaces = new(SurfacesName);
    public static readonly ActivitySource Data = new(DataName);
}
