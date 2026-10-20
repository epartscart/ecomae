namespace EcomAE.Platform.Configuration;

/// <summary>MySqlConnector pool / timeout knobs for tenant opens.</summary>
public sealed class TenantDbPoolOptions
{
    public const string SectionName = "EcomAE:TenantDbPool";

    public int MaximumPoolSize { get; set; } = 32;

    // Keep a few warm connections per tenant pool so idle-then-busy requests
    // do not pay the TCP+auth handshake on a cold pool. 0 = cold start (default).
    public int MinimumPoolSize { get; set; } = 0;

    public int ConnectionTimeoutSeconds { get; set; } = 8;

    public int DefaultCommandTimeoutSeconds { get; set; } = 30;

    public int OpenRetryAttempts { get; set; } = 3;

    public int OpenRetryDelayMilliseconds { get; set; } = 150;
}
