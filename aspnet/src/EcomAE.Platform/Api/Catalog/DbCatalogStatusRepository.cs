using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Read-only catalog status reader mirroring PHP <c>epc_status_payload()</c>.
/// Performs zero writes.
/// </summary>
public sealed class DbCatalogStatusRepository : ICatalogStatusRepository
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbCatalogStatusRepository(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CatalogStatusPayload> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return await new MigrationCatalogStatusRepository().GetStatusAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var connection = await _connections.OpenAsync(databaseName: null, cancellationToken).ConfigureAwait(false);

        var connected = false;
        var statusCode = 0;
        var message = "No Epart catalog check saved yet.";
        long lastChecked = 0;
        long lastSuccess = 0;
        long lastError = 0;

        await using (var statusCommand = connection.CreateCommand())
        {
            statusCommand.CommandText = LegacyCatalogStatusSql.SelectSyncStatus;
            await using var reader = await statusCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                connected = Convert.ToInt32(reader["connected"], CultureInfo.InvariantCulture) == 1;
                statusCode = Convert.ToInt32(reader["status_code"], CultureInfo.InvariantCulture);
                message = Convert.ToString(reader["message"], CultureInfo.InvariantCulture) ?? message;
                lastChecked = Convert.ToInt64(reader["last_checked"], CultureInfo.InvariantCulture);
                lastSuccess = Convert.ToInt64(reader["last_success"], CultureInfo.InvariantCulture);
                lastError = Convert.ToInt64(reader["last_error"], CultureInfo.InvariantCulture);
            }
        }

        var manufacturers = await ScalarCountAsync(connection, LegacyCatalogStatusSql.CountManufacturers, cancellationToken).ConfigureAwait(false);
        var models = await ScalarCountAsync(connection, LegacyCatalogStatusSql.CountModels, cancellationToken).ConfigureAwait(false);
        var modifications = await ScalarCountAsync(connection, LegacyCatalogStatusSql.CountModifications, cancellationToken).ConfigureAwait(false);
        var brands = await ScalarCountAsync(connection, LegacyCatalogStatusSql.CountBrands, cancellationToken).ConfigureAwait(false);
        var vins = await ScalarCountAsync(connection, LegacyCatalogStatusSql.CountVinCache, cancellationToken).ConfigureAwait(false);
        var cacheRows = await ScalarCountAsync(connection, LegacyCatalogStatusSql.CountCacheRows, cancellationToken).ConfigureAwait(false);
        var sections = await ReadSectionsAsync(connection, cancellationToken).ConfigureAwait(false);

        var offlineReady = manufacturers >= 20 || cacheRows >= 5;
        var actions = new List<string>();
        if (!connected && !offlineReady)
        {
            actions.Add("Run /epc-offline-resilience-warm.php while Epart catalog is online to save catalog data.");
        }

        if (!connected && offlineReady)
        {
            actions.Add("Epart catalog offline — site will use saved catalog. Re-run warm script when the service is back.");
        }

        if (vins < 5)
        {
            actions.Add("Run /epc-offline-resilience-warm.php?vin=1 to save VIN decode data while Epart catalog is available.");
        }

        return new CatalogStatusPayload(
            connected,
            message,
            lastChecked,
            lastSuccess,
            lastError,
            statusCode,
            new CatalogStatusCounts(manufacturers, models, modifications, brands, vins),
            sections,
            cacheRows,
            offlineReady,
            actions,
            Source: "database");
    }

    private static async Task<int> ScalarCountAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadSectionsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var sections = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyCatalogStatusSql.CountManufacturersBySection;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var section = Convert.ToString(reader["section"], CultureInfo.InvariantCulture) ?? string.Empty;
            sections[section] = Convert.ToInt32(reader["cnt"], CultureInfo.InvariantCulture);
        }

        return sections;
    }
}
