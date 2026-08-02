using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Read-only offline cache reader for VIN and generic UMAPI action cache rows.
/// Performs zero writes.
/// </summary>
public sealed class DbCatalogOfflineCacheRepository : ICatalogOfflineCacheRepository
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbCatalogOfflineCacheRepository(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CatalogVinCacheRow?> FindVinAsync(string vin, string language, string region, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(vin))
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyCatalogVinSql.SelectByVinLanguageRegion;
        AddParameter(command, "@vin", vin);
        AddParameter(command, "@language", string.IsNullOrWhiteSpace(language) ? "en" : language);
        AddParameter(command, "@region", string.IsNullOrWhiteSpace(region) ? "WWW" : region);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CatalogVinCacheRow(
            Convert.ToString(reader["vin"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["language"], CultureInfo.InvariantCulture) ?? "en",
            Convert.ToString(reader["region"], CultureInfo.InvariantCulture) ?? "WWW",
            Convert.ToString(reader["response_json"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt32(reader["vehicle_count"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["manufacturer"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["model_label"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["http_status"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["updated_at"], CultureInfo.InvariantCulture));
    }

    public async Task<CatalogActionCacheRow?> FindActionCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(cacheKey))
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyUmapiActionCacheSql.SelectByCacheKey;
        AddParameter(command, "@cacheKey", cacheKey);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CatalogActionCacheRow(
            Convert.ToString(reader["cache_key"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["action"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["section"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["language"], CultureInfo.InvariantCulture) ?? "en",
            Convert.ToString(reader["region"], CultureInfo.InvariantCulture) ?? "WWW",
            Convert.ToString(reader["response_json"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt32(reader["rows_count"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["http_status"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["last_sync"], CultureInfo.InvariantCulture));
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
