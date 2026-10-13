using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpApSetupRow(
    long Id,
    string Code,
    string Name,
    string Extra,
    bool Active);

public sealed record ErpApSetupSnapshot(
    IReadOnlyList<ErpApSetupRow> Methods,
    IReadOnlyList<ErpApSetupRow> Terms,
    IReadOnlyList<ErpApSetupRow> Groups,
    string Source,
    string Message);

public interface IErpApSetupReadService
{
    Task<ErpApSetupSnapshot> LoadAsync(int limit = 200, CancellationToken cancellationToken = default);
}

/// <summary>AP setup masters: payment methods, payment terms, vendor groups.</summary>
public sealed class ErpApSetupReadService : IErpApSetupReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpApSetupReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpApSetupSnapshot> LoadAsync(int limit = 200, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], [], "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsurePmMastersAsync(connection, cancellationToken).ConfigureAwait(false);
        var methods = await ReadAsync(
            connection,
            "epc_erp_pm_pay_methods",
            "CONCAT(IFNULL(`method_type`,''), CASE WHEN IFNULL(`account_code`,'')='' THEN '' ELSE CONCAT(' · ',`account_code`) END)",
            cancellationToken,
            limit).ConfigureAwait(false);
        var terms = await ReadAsync(
            connection,
            "epc_erp_pm_pay_terms",
            "CAST(IFNULL(`net_days`,0) AS CHAR)",
            cancellationToken,
            limit).ConfigureAwait(false);
        var groups = await ReadAsync(
            connection,
            "epc_erp_pm_vendor_groups",
            "CAST(IFNULL(`terms_id`,0) AS CHAR)",
            cancellationToken,
            limit).ConfigureAwait(false);
        return new(methods, terms, groups, "database", string.Empty);
    }

    private static async Task<IReadOnlyList<ErpApSetupRow>> ReadAsync(
        DbConnection connection,
        string table,
        string extraSql,
        CancellationToken cancellationToken,
        int limit)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit, 1, 500);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `id`, IFNULL(`code`,''), IFNULL(`name`,''), " + extraSql + ", IFNULL(`active`,1) "
            + "FROM `" + table + "` ORDER BY `name`, `id` LIMIT " + safeLimit.ToString(CultureInfo.InvariantCulture);
        var rows = new List<ErpApSetupRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(3) ? string.Empty : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                !reader.IsDBNull(4) && Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0));
        }

        return rows;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }
}
