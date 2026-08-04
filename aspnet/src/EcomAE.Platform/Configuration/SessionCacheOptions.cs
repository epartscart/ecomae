namespace EcomAE.Platform.Configuration;

/// <summary>
/// Process-local admin identity/session cache. Does not replace PHP cookies.
/// </summary>
public sealed class SessionCacheOptions
{
    public const string SectionName = "EcomAE:SessionCache";

    /// <summary>When false, every request hits MySQL for session/ACL (legacy behavior).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>TTL for admin identity cache entries (seconds).</summary>
    public int IdentityTtlSeconds { get; set; } = 45;

    /// <summary>TTL for session-exists cache entries (seconds).</summary>
    public int SessionExistsTtlSeconds { get; set; } = 20;
}
