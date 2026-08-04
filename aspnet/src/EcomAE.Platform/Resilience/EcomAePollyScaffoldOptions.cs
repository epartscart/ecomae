namespace EcomAE.Platform.Resilience;

/// <summary>
/// Polly resilience pipeline scaffolding options (timeout/retry/circuit-breaker).
/// Not bound in <c>Program.cs</c>; policies are not registered in this step.
/// </summary>
public sealed class EcomAePollyScaffoldOptions
{
    public const string SectionName = "EcomAe:Polly";

    public int TimeoutMilliseconds { get; set; } = 10_000;

    public int RetryCount { get; set; } = 2;

    public bool Enabled { get; set; }

    /// <summary>Always false until staging policy composition is approved.</summary>
    public bool RegisterPipelines { get; set; }
}
