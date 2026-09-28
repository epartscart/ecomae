using System.Data.Common;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Migration;

public sealed record ExpandContractMigration(
    string Id,
    string Phase,
    string Description,
    string Sql);

public interface IExpandContractMigrationRunner
{
    IReadOnlyList<ExpandContractMigration> GetPlan();

    Task ApplyAsync(CancellationToken cancellationToken = default);
}

public sealed class ExpandContractMigrationRunner : IExpandContractMigrationRunner
{
    private const string TrackingTable = "ecomae_aspnet_schema_migrations";
    private readonly ITenantDbConnectionFactory _connections;
    private readonly SchemaMigrationOptions _options;

    public ExpandContractMigrationRunner(
        ITenantDbConnectionFactory connections,
        IOptions<SchemaMigrationOptions> options)
    {
        _connections = connections;
        _options = options.Value;
    }

    public IReadOnlyList<ExpandContractMigration> GetPlan() =>
    [
        new(
            "20260927-001",
            "expand",
            "Add the deployment-owned migration ledger before feature columns are introduced.",
            $"""
            CREATE TABLE IF NOT EXISTS `{TrackingTable}` (
                `migration_id` varchar(120) NOT NULL,
                `phase` varchar(32) NOT NULL,
                `applied_at_utc` datetime(6) NOT NULL,
                PRIMARY KEY (`migration_id`)
            ) ENGINE=InnoDB
            """),
        new(
            "20260927-002",
            "contract",
            "Contract work is intentionally deferred until all readers and writers use expanded columns.",
            "-- No destructive operation. Retire columns only in a separately approved release.")
    ];

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new InvalidOperationException("Schema migrations require a configured registry connection.");
        }

        await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureLockTimeoutAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var plan = GetPlan();
        await ExecuteAsync(connection, transaction, plan[0].Sql, cancellationToken).ConfigureAwait(false);
        foreach (var migration in plan)
        {
            if (await IsAppliedAsync(connection, transaction, migration.Id, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(migration.Sql) && !migration.Sql.StartsWith("--", StringComparison.Ordinal))
            {
                await ExecuteAsync(connection, transaction, migration.Sql, cancellationToken).ConfigureAwait(false);
            }

            await ExecuteAsync(
                connection,
                transaction,
                $"INSERT INTO `{TrackingTable}` (`migration_id`,`phase`,`applied_at_utc`) VALUES (@id,@phase,UTC_TIMESTAMP(6))",
                cancellationToken,
                ("@id", migration.Id),
                ("@phase", migration.Phase)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ConfigureLockTimeoutAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Clamp(_options.LockTimeoutSeconds, 1, 300);
        await using var command = connection.CreateCommand();
        command.CommandText = "SET SESSION lock_wait_timeout = @timeout";
        AddParameter(command, "@timeout", timeoutSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsAppliedAsync(
        DbConnection connection,
        DbTransaction transaction,
        string id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT 1 FROM `{TrackingTable}` WHERE `migration_id` = @id LIMIT 1";
        AddParameter(command, "@id", id);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            AddParameter(command, parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

public sealed class ExpandContractMigrationHostedService : IHostedService
{
    private readonly IExpandContractMigrationRunner _runner;
    private readonly SchemaMigrationOptions _options;
    private readonly ILogger<ExpandContractMigrationHostedService> _logger;

    public ExpandContractMigrationHostedService(
        IExpandContractMigrationRunner runner,
        IOptions<SchemaMigrationOptions> options,
        ILogger<ExpandContractMigrationHostedService> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.ApplyOnStartup)
        {
            return;
        }

        _logger.LogInformation("Applying expand/contract schema migrations.");
        await _runner.ApplyAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
