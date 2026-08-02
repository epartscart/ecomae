using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Read-only catalog cache reader for models/modifications/brands.
/// Mirrors PHP epc_cached_* helpers. Performs zero writes.
/// </summary>
public sealed class DbCatalogVehicleCacheRepository : ICatalogVehicleCacheRepository
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbCatalogVehicleCacheRepository(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<CatalogModelRow>> FindModelsAsync(string section, int mfaId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(section) || mfaId <= 0)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyCatalogModelsSql.SelectBySectionAndMfa;
        AddParameter(command, "@section", section.Trim());
        AddParameter(command, "@mfaId", mfaId);

        var rows = new List<CatalogModelRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogModelRow(
                Convert.ToString(reader["section"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["mfa_id"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["ms_id"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["model_series"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["year_from"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["year_to"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["raw_json"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["updated_at"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CatalogModificationRow>> FindModificationsAsync(string section, int msId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(section) || msId <= 0)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyCatalogModificationsSql.SelectBySectionAndMs;
        AddParameter(command, "@section", section.Trim());
        AddParameter(command, "@msId", msId);

        var rows = new List<CatalogModificationRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogModificationRow(
                Convert.ToString(reader["section"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["ms_id"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["modification_id"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["year_from"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["year_to"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["power_kw"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["capacity_lt"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["fuel_type"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["raw_json"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["updated_at"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CatalogBrandRow>> FindBrandsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyCatalogBrandsSql.SelectAll;

        var rows = new List<CatalogBrandRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CatalogBrandRow(
                Convert.ToInt32(reader["sup_id"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["brand"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["full_name"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["raw_json"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["updated_at"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    public static object? DecodeRawJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(rawJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
