namespace EcomAE.Platform.Caching;

/// <summary>
/// Redis 8 scaffolding options for future distributed cache / rate-limit / session materialization.
/// Not bound in <c>Program.cs</c> and must not replace PHP session cookies until cookie parity evidence exists.
/// </summary>
public sealed class EcomAeRedisScaffoldOptions
{
    public const string SectionName = "EcomAe:Redis";

    /// <summary>Connection string placeholder only — never commit secrets.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string KeyPrefix { get; set; } = "ecomae:";

    public bool Enabled { get; set; }

    /// <summary>Always false in scaffolding — PHP cookies remain authoritative.</summary>
    public bool ReplacePhpSessionCookies { get; set; }
}
