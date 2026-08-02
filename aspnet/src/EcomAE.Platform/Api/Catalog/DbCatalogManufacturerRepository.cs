using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Read-only manufacturer cache reader mirroring PHP <c>epc_cached_manufacturers_payload</c>.
/// Performs zero writes.
/// </summary>
public sealed class DbCatalogManufacturerRepository : ICatalogManufacturerRepository
{
    private readonly ITenantDbConnectionFactory _connections;

    public DbCatalogManufacturerRepository(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<CatalogManufacturerRow>> FindBySectionAsync(string section, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(section))
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(databaseName: null, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacyCatalogManufacturersSql.SelectBySection;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@section";
        parameter.Value = section.Trim();
        command.Parameters.Add(parameter);

        var rows = new List<CatalogManufacturerRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(Map(reader));
        }

        return rows;
    }

    private static CatalogManufacturerRow Map(DbDataReader reader)
    {
        return new CatalogManufacturerRow(
            Convert.ToString(reader["section"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt32(reader["mfa_id"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["manufacturer_ru"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["type"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["country"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["popular"], CultureInfo.InvariantCulture) == 1,
            Convert.ToInt32(reader["is_logo"], CultureInfo.InvariantCulture) == 1,
            Convert.ToString(reader["raw_json"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["updated_at"], CultureInfo.InvariantCulture));
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
}
