using System.Diagnostics;

namespace EcomAE.Workers.Observability;

/// <summary>
/// Stable ActivitySource names for workers OpenTelemetry wiring (mirrors platform scaffolding).
/// Exporters are not registered; dry-run workers must not claim production telemetry.
/// </summary>
public static class EcomAeWorkerActivitySources
{
    public const string WorkersName = "EcomAE.Workers";

    public static readonly ActivitySource Workers = new(WorkersName);
}
