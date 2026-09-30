using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpOrganizationMasterRow(
    long Id,
    string Code,
    string Name,
    long LegalEntityId,
    long ParentId,
    long DimensionId,
    bool Active);

public sealed record ErpOrganizationMasterSnapshot(
    IReadOnlyList<ErpOrganizationMasterRow> LegalEntities,
    IReadOnlyList<ErpOrganizationMasterRow> BusinessUnits,
    IReadOnlyList<ErpOrganizationMasterRow> Dimensions,
    IReadOnlyList<ErpOrganizationMasterRow> DimensionValues,
    string Source,
    string Message);

public interface IErpOrganizationMasterReadService
{
    Task<ErpOrganizationMasterSnapshot> LoadAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed class ErpOrganizationMasterReadService : IErpOrganizationMasterReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpOrganizationMasterReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpOrganizationMasterSnapshot> LoadAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var entities = await ReadAsync(connection, "epc_erp_pm_legal_entities", "0", "0", cancellationToken, limit).ConfigureAwait(false);
        var units = await ReadAsync(connection, "epc_erp_pm_business_units", "legal_entity_id", "parent_id", cancellationToken, limit).ConfigureAwait(false);
        var dimensions = await ReadAsync(connection, "epc_erp_pm_dimensions", "0", "0", cancellationToken, limit).ConfigureAwait(false);
        var values = await ReadAsync(connection, "epc_erp_pm_dimension_values", "dimension_id", "0", cancellationToken, limit).ConfigureAwait(false);
        return new(entities, units, dimensions, values, "database", string.Empty);
    }

    private static ErpOrganizationMasterSnapshot Empty(string message)
        => new([], [], [], [], "migration", message);

    private static async Task<IReadOnlyList<ErpOrganizationMasterRow>> ReadAsync(
        DbConnection connection,
        string table,
        string legalEntityColumn,
        string parentColumn,
        CancellationToken cancellationToken,
        int limit)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var legalEntity = legalEntityColumn == "0" ? "0" : "IFNULL(`" + legalEntityColumn + "`,0)";
        var parent = parentColumn == "0" ? "0" : "IFNULL(`" + parentColumn + "`,0)";
        var dimension = table == "epc_erp_pm_dimension_values" ? "IFNULL(`dimension_id`,0)" : "0";
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`,IFNULL(`code`,''),IFNULL(`name`,'')," + legalEntity + "," + parent + "," + dimension + ",IFNULL(`active`,1) FROM `" + table + "` ORDER BY `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        var rows = new List<ErpOrganizationMasterRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture) != 0));
        }
        return rows;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var value = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return value > 0;
    }
}
