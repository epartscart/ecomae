namespace EcomAE.Platform.Configuration;

/// <summary>MySqlConnector pool / timeout knobs for tenant opens.</summary>
public sealed class TenantDbPoolOptions
{
    public const string SectionName = "EcomAE:TenantDbPool";

    public int MaximumPoolSize { get; set; } = 32;

    public int ConnectionTimeoutSeconds { get; set; } = 8;

    public int DefaultCommandTimeoutSeconds { get; set; } = 30;
}
