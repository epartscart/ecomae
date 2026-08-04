namespace EcomAE.Platform.Security.Scaffolding;

/// <summary>
/// Rate-limit scaffolding options for future ASP.NET rate limiter / Redis-backed limits.
/// Not bound in <c>Program.cs</c>. Existing API-client throttling remains current path.
/// </summary>
public sealed class EcomAeRateLimitScaffoldOptions
{
    public const string SectionName = "EcomAe:RateLimit";

    public int PermitLimit { get; set; } = 100;

    public int WindowSeconds { get; set; } = 60;

    public bool Enabled { get; set; }

    /// <summary>Always false until staging limiter policy is approved.</summary>
    public bool ReplaceLegacyApiClientThrottle { get; set; }
}
