using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_multi_entity_set</c> / ajax <c>multi_entity_save</c> twin.
/// UPSERT <c>epc_erp_platform_settings.multi_entity_enabled</c>.
/// Group/member/IC writes and schema ensure stay on their own paths.
/// Does not CREATE tables.
/// </summary>
public interface IErpMultiEntitySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpMultiEntitySaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpMultiEntitySaveWriteRequest(int? Enabled = null);

public sealed class ErpMultiEntitySaveWriteService : IErpMultiEntitySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMultiEntitySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpMultiEntitySaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var enabled = request.Enabled is > 0 ? 1 : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_platform_settings", "setting_key", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_platform_settings", "setting_value", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Platform settings table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_platform_settings` (`setting_key`, `setting_value`, `time_updated`) VALUES (?,?,?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `time_updated` = VALUES(`time_updated`)"),
            cancellationToken,
            "multi_entity_enabled",
            enabled == 1 ? "1" : "0",
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Multi-entity preference saved", 0);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
