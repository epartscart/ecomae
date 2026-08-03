namespace EcomAE.Platform.Observability;

/// <summary>
/// Serilog / OTLP sink scaffolding options.
/// Not bound in <c>Program.cs</c>; exporters/sinks are not registered in this step.
/// </summary>
public sealed class EcomAeSerilogScaffoldOptions
{
    public const string SectionName = "EcomAe:Serilog";

    public string MinimumLevel { get; set; } = "Information";

    public string OtlpEndpoint { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    /// <summary>Always false until OTLP/Seq sinks are approved for staging.</summary>
    public bool RegisterExporters { get; set; }
}
