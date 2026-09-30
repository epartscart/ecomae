using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpDimensionCatalogOption(long Id, string Code, string Name);

public sealed record ErpDimensionCatalog(
    string Key,
    string Label,
    IReadOnlyList<ErpDimensionCatalogOption> Options);

public interface IErpDimensionCatalogReadService
{
    Task<IReadOnlyList<ErpDimensionCatalog>> LoadAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed class ErpDimensionCatalogReadService : IErpDimensionCatalogReadService
{
    private static readonly (string Key, string Label, string Table)[] FixedDimensions =
    [
        ("business_unit", "Business unit", "epc_erp_pm_business_units"),
        ("legal_entity", "Legal entity", "epc_erp_pm_legal_entities"),
        ("class_unit", "Class unit", "epc_erp_pm_class_units")
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpDimensionCatalogReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpDimensionCatalog>> LoadAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        var boundedLimit = Math.Clamp(limit, 1, 500);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<ErpDimensionCatalog>();

        foreach (var (key, label, table) in FixedDimensions)
        {
            var options = await ReadOptionsAsync(connection, table, boundedLimit, cancellationToken).ConfigureAwait(false);
            if (options.Count > 0)
            {
                result.Add(new(key, label, options));
            }
        }

        if (await TableExistsAsync(connection, "epc_erp_pm_dimensions", cancellationToken).ConfigureAwait(false)
            && await TableExistsAsync(connection, "epc_erp_pm_dimension_values", cancellationToken).ConfigureAwait(false))
        {
            var dimensions = await ReadOptionsAsync(connection, "epc_erp_pm_dimensions", boundedLimit, cancellationToken).ConfigureAwait(false);
            foreach (var dimension in dimensions)
            {
                var values = await ReadDimensionValuesAsync(connection, dimension.Id, boundedLimit, cancellationToken).ConfigureAwait(false);
                if (values.Count > 0)
                {
                    result.Add(new(
                        "dim" + dimension.Id.ToString(CultureInfo.InvariantCulture),
                        dimension.Code.Length == 0 ? dimension.Name : dimension.Code + " · " + dimension.Name,
                        values));
                }
            }
        }

        return result;
    }

    private static async Task<List<ErpDimensionCatalogOption>> ReadOptionsAsync(
        DbConnection connection,
        string table,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`,IFNULL(`code`,''),IFNULL(`name`,'') FROM `" + table
            + "` WHERE IFNULL(`active`,1) = 1 ORDER BY `code`,`name` LIMIT "
            + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<ErpDimensionCatalogOption>> ReadDimensionValuesAsync(
        DbConnection connection,
        long dimensionId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,IFNULL(`code`,''),IFNULL(`name`,'') FROM `epc_erp_pm_dimension_values` " +
            "WHERE `dimension_id` = ? AND IFNULL(`active`,1) = 1 ORDER BY `code`,`name` LIMIT " +
            limit.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, dimensionId);
        return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<ErpDimensionCatalogOption>> ReadRowsAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpDimensionCatalogOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return rows;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }
}
