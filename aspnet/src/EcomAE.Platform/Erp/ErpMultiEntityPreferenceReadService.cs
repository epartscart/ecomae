using System.Data.Common;

namespace EcomAE.Platform.Erp;

public sealed record ErpMultiEntityPreferenceSnapshot(
    bool Enabled,
    string Source,
    string Message);

public interface IErpMultiEntityPreferenceReadService
{
    Task<ErpMultiEntityPreferenceSnapshot> LoadAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ErpMultiEntityPreferenceReadService : IErpMultiEntityPreferenceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMultiEntityPreferenceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpMultiEntityPreferenceSnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_platform_settings", "setting_key", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_platform_settings", "setting_value", cancellationToken).ConfigureAwait(false))
        {
            return Empty("Platform settings table is not provisioned.");
        }

        var value = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `setting_value` FROM `epc_erp_platform_settings` WHERE `setting_key` = ? LIMIT 1"),
            cancellationToken,
            "multi_entity_enabled").ConfigureAwait(false);

        return new ErpMultiEntityPreferenceSnapshot(
            string.Equals(value?.Trim(), "1", StringComparison.Ordinal),
            "database",
            "");
    }

    private static ErpMultiEntityPreferenceSnapshot Empty(string message)
        => new(false, "migration", message);

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return count > 0;
    }
}
