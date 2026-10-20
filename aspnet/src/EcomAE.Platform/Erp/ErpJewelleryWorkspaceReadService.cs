using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Read model for the jewellery workspace page — metal stock, purchase
/// and sales vouchers, repairs and issued invoices, filterable by metal/karat
/// and period. Reads the real epc_jewel_* tables the INDJ module writes.
/// Mirrors <see cref="ErpFitOutWorkspaceReadService"/>.</summary>
public interface IErpJewelleryWorkspaceReadService
{
    Task<ErpJewelleryWorkspaceSnapshot> LoadAsync(string? metal, string? period, CancellationToken cancellationToken = default);
}

public sealed class ErpJewelleryWorkspaceSnapshot
{
    public IReadOnlyList<string> Metals { get; set; } = [];
    public IReadOnlyList<(string Label, string Value, string Hint)> Kpis { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Stock { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Purchases { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Sales { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> VoucherLines { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Repairs { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Invoices { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> LowStock { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Ledger { get; set; } = [];
    public string Message { get; set; } = "";
}

public sealed class ErpJewelleryWorkspaceReadService : IErpJewelleryWorkspaceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJewelleryWorkspaceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpJewelleryWorkspaceSnapshot> LoadAsync(string? metal, string? period, CancellationToken cancellationToken = default)
    {
        var snapshot = new ErpJewelleryWorkspaceSnapshot();
        if (!_connections.IsConfigured)
        {
            snapshot.Message = "TenantRegistry DB is not configured.";
            return snapshot;
        }

        var (fromUtc, toUtc) = PeriodWindow(period);
        var fromIso = fromUtc > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromUtc).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        var toIso = toUtc > 0 ? DateTimeOffset.FromUnixTimeSeconds(toUtc).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpJewelleryWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        var met = string.IsNullOrWhiteSpace(metal) ? "" : metal.Trim();
        snapshot.Metals = await LoadMetalsAsync(connection, cancellationToken).ConfigureAwait(false);

        var salesTypes = string.Join("','", ErpJwVocTypes.SalesAnalysisAliases);
        var purchaseTypes = string.Join("','", ErpJwVocTypes.MetalPurchaseAliases.Concat(ErpJwVocTypes.DiamondPurchaseAliases));

        snapshot.Kpis =
        [
            ("Stock items", await ScalarAsync(connection,
                "SELECT COUNT(*) FROM `epc_jewel_metal_stock` WHERE (?='' OR `metal`=? OR `karat`=?)", met, met, met), "active metal-stock codes"),
            ("Stock weight", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`stock_gms`),0) FROM `epc_jewel_metal_stock` WHERE (?='' OR `metal`=? OR `karat`=?)", met, met, met), "grams on hand"),
            ("Stock value", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`stock_value`),0) FROM `epc_jewel_metal_stock` WHERE (?='' OR `metal`=? OR `karat`=?)", met, met, met), "at cost"),
            ("Sales in period", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`total_with_vat`),0) FROM `epc_jewel_voucher` WHERE `voc_type` IN ('" + salesTypes + "')"
                + " AND (? IS NULL OR `voc_date`>=?) AND (? IS NULL OR `voc_date`<?)", fromIso, fromIso, toIso, toIso), "invoiced + counter sales"),
            ("Repairs open", await ScalarAsync(connection,
                "SELECT COUNT(*) FROM `epc_jewel_repair` WHERE `status` NOT IN ('delivered','cancelled')"), "jobs in workshop"),
        ];

        snapshot.Stock = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `item_code`,`description`,`metal`,`karat`,`type`,`brand`,`category`,"
            + " CAST(`pur_cost_gms` AS CHAR), CAST(`sale_price_gms` AS CHAR), `stock_qty`, CAST(`stock_gms` AS CHAR), CAST(`stock_value` AS CHAR)"
            + " FROM `epc_jewel_metal_stock` WHERE (?='' OR `metal`=? OR `karat`=?) ORDER BY `metal`,`karat`,`item_code` LIMIT 400",
            met, met, met);

        snapshot.Purchases = await LoadVouchersAsync(connection, purchaseTypes, fromIso, toIso, cancellationToken);
        snapshot.Sales = await LoadVouchersAsync(connection, salesTypes, fromIso, toIso, cancellationToken);

        snapshot.VoucherLines = await LoadRowsAsync(connection, cancellationToken,
            "SELECT l.`id`, v.`voc_type`, v.`voc_no`, l.`stock_code`, l.`description`, CAST(l.`gr_wt` AS CHAR),"
            + " CAST(l.`mkg_amount` AS CHAR), CAST(l.`total_with_vat` AS CHAR)"
            + " FROM `epc_jewel_voucher_lines` l JOIN `epc_jewel_voucher` v ON v.`id`=l.`voucher_id`"
            + " WHERE (? IS NULL OR v.`voc_date`>=?) AND (? IS NULL OR v.`voc_date`<?) ORDER BY l.`id` DESC LIMIT 400",
            fromIso, fromIso, toIso, toIso);

        snapshot.Repairs = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `id`, `voc_no`, `customer_name`, `mobile`, `repair_narration`, `status`,"
            + " DATE_FORMAT(`delivery_date`,'%Y-%m-%d'), DATE_FORMAT(`voc_date`,'%Y-%m-%d')"
            + " FROM `epc_jewel_repair` ORDER BY `id` DESC LIMIT 200");

        snapshot.Invoices = await LoadRowsAsync(connection, cancellationToken,
            "SELECT k.`id`, k.`voucher_id`, k.`invoice_number`, CAST(k.`net` AS CHAR), CAST(k.`vat` AS CHAR), CAST(k.`gross` AS CHAR),"
            + " CONCAT('/erp/invoice-print?id=', CAST(k.`invoice_id` AS CHAR))"
            + " FROM `ecomae_jw_invoice_links` k ORDER BY k.`id` DESC LIMIT 200");

        snapshot.LowStock = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `item_code`,`description`,`metal`,`karat`,`stock_qty`,`reorder_level`"
            + " FROM `epc_jewel_metal_stock` WHERE `reorder_level`>0 AND `stock_qty`<=`reorder_level` AND (?='' OR `metal`=? OR `karat`=?)"
            + " ORDER BY `stock_qty` LIMIT 200",
            met, met, met);

        snapshot.Ledger = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `id`, `reference`, `description`, FROM_UNIXTIME(`journal_date`,'%Y-%m-%d')"
            + " FROM `epc_erp_gl_journals` WHERE `source_type`='adjustment' AND `reference` LIKE 'JW-%' AND `active`=1"
            + " ORDER BY `id` DESC LIMIT 100");

        return snapshot;
    }

    private static async Task<IReadOnlyList<IReadOnlyList<string>>> LoadVouchersAsync(
        DbConnection connection, string types, string? fromIso, string? toIso, CancellationToken ct)
        => await LoadRowsAsync(connection, ct,
            "SELECT `id`, `voc_type`, `voc_no`, `party_name`, `customer_name`, `status`,"
            + " CAST(`net_amount` AS CHAR), CAST(`vat_amount` AS CHAR), CAST(`total_with_vat` AS CHAR), DATE_FORMAT(`voc_date`,'%Y-%m-%d')"
            + " FROM `epc_jewel_voucher` WHERE `voc_type` IN ('" + types + "')"
            + " AND (? IS NULL OR `voc_date`>=?) AND (? IS NULL OR `voc_date`<?) ORDER BY `id` DESC LIMIT 400",
            fromIso, fromIso, toIso, toIso);

    private static async Task<IReadOnlyList<string>> LoadMetalsAsync(DbConnection connection, CancellationToken ct)
    {
        var list = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT DISTINCT m FROM (SELECT `metal` AS m FROM `epc_jewel_metal_stock` WHERE `metal`<>''"
            + " UNION SELECT `karat` FROM `epc_jewel_metal_stock` WHERE `karat`<>'') t ORDER BY m";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    private static async Task<string> ScalarAsync(DbConnection connection, string sql, params object?[] args)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args!);
        var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return value switch
        {
            null or DBNull => "0",
            decimal d => d.ToString("N2", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        };
    }

    private static async Task<IReadOnlyList<IReadOnlyList<string>>> LoadRowsAsync(
        DbConnection connection, CancellationToken ct, string sql, params object?[] args)
    {
        var rows = new List<IReadOnlyList<string>>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args!);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
            }
            rows.Add(row);
        }
        return rows;
    }

    private static (long FromUtc, long ToUtc) PeriodWindow(string? period)
    {
        if (!string.IsNullOrWhiteSpace(period)
            && DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var from = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var to = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).ToUnixTimeSeconds();
            return (from, to);
        }
        return (0, 0);
    }
}

/// <summary>Creates the jewellery workspace's own tables — the invoice-link
/// register; domain rows live in the shared epc_jewel_* schema.</summary>
public static class ErpJewelleryWorkspaceSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `ecomae_jw_invoice_links` ("
            + " `id` bigint NOT NULL AUTO_INCREMENT,"
            + " `voucher_id` bigint NOT NULL,"
            + " `invoice_id` bigint NOT NULL DEFAULT 0,"
            + " `invoice_number` varchar(64) NOT NULL DEFAULT '',"
            + " `net` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `vat` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `gross` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `created_at_utc` bigint NOT NULL,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_voucher` (`voucher_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
    }
}
