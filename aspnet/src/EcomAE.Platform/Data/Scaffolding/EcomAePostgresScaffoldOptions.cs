namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// PostgreSQL 17 target SoR scaffolding options.
/// Not bound in <c>Program.cs</c>. Legacy MySQL/MariaDB via MySqlConnector remains the bridge SoR.
/// Do not claim PG live until migration + dual-sample parity evidence exist.
/// </summary>
public sealed class EcomAePostgresScaffoldOptions
{
    public const string SectionName = "EcomAe:Postgres";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "ecomae";

    public string Username { get; set; } = string.Empty;

    /// <summary>Placeholder only — never commit secrets.</summary>
    public string Password { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    /// <summary>Always false until SoR cutover is approved with evidence.</summary>
    public bool ReplaceMysqlBridge { get; set; }
}
