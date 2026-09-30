using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpDimensionLink(
    string Key,
    long ValueId,
    string ValueCode,
    string ValueLabel,
    long TimeCreated);

public interface IErpDimensionLinkReadService
{
    Task<IReadOnlyList<ErpDimensionLink>> LoadAsync(
        string? entityType,
        long entityId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpDimensionLinkReadService : IErpDimensionLinkReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpDimensionLinkReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpDimensionLink>> LoadAsync(
        string? entityType,
        long entityId,
        CancellationToken cancellationToken = default)
    {
        var type = (entityType ?? string.Empty).Trim().ToLowerInvariant();
        if (type.Length == 0 || entityId <= 0 || !_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `dim_key`,`ref_id`,IFNULL(`value_code`,''),IFNULL(`value_label`,''),IFNULL(`time_created`,0)
            FROM `epc_erp_dim_links`
            WHERE `entity_type` = ? AND `entity_id` = ?
            ORDER BY `dim_key`
            """);
        ErpDb.AddParameters(command, type, entityId);

        var links = new List<ErpDimensionLink>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            links.Add(new(
                Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture)));
        }

        return links;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            "epc_erp_dim_links").ConfigureAwait(false) > 0;
}
