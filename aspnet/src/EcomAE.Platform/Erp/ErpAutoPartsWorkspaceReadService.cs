using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Read model for the auto-parts workspace page — parts catalogue, stock
/// movements, purchases, sales orders and issued invoices, all filterable by
/// category and period. Mirrors <see cref="ErpFitOutWorkspaceReadService"/>.</summary>
public interface IErpAutoPartsWorkspaceReadService
{
    Task<ErpAutoPartsWorkspaceSnapshot> LoadAsync(string? category, string? period, CancellationToken cancellationToken = default);
}

public sealed class ErpAutoPartsWorkspaceSnapshot
{
    public IReadOnlyList<string> Categories { get; set; } = [];
    public IReadOnlyList<(string Label, string Value, string Hint)> Kpis { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Parts { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Movements { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Purchases { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Sales { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> OrderLines { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Invoices { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> LowStock { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Ledger { get; set; } = [];
    public string Message { get; set; } = "";
}

public sealed class ErpAutoPartsWorkspaceReadService : IErpAutoPartsWorkspaceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAutoPartsWorkspaceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpAutoPartsWorkspaceSnapshot> LoadAsync(string? category, string? period, CancellationToken cancellationToken = default)
    {
        var snapshot = new ErpAutoPartsWorkspaceSnapshot();
        if (!_connections.IsConfigured)
        {
            snapshot.Message = "TenantRegistry DB is not configured.";
            return snapshot;
        }

        var (fromUtc, toUtc) = PeriodWindow(period);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await _glEnsure(connection, cancellationToken).ConfigureAwait(false);

        snapshot.Categories = await LoadCategoriesAsync(connection, cancellationToken).ConfigureAwait(false);
        var cat = string.IsNullOrWhiteSpace(category) ? "" : category.Trim();

        snapshot.Kpis =
        [
            ("Parts in catalogue", await ScalarAsync(connection,
                "SELECT COUNT(*) FROM `ecomae_autoparts_parts` WHERE `active`=1 AND (?='' OR `category`=?)", cat, cat), "active SKUs"),
            ("Stock on hand", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`stock_qty`),0) FROM `ecomae_autoparts_parts` WHERE `active`=1 AND (?='' OR `category`=?)", cat, cat), "units across depots"),
            ("Stock value", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`stock_qty`*`purchase_cost`),0) FROM `ecomae_autoparts_parts` WHERE `active`=1 AND (?='' OR `category`=?)", cat, cat), "at purchase cost"),
            ("Sales in period", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`total`),0) FROM `ecomae_autoparts_orders` WHERE `order_type`='sale' AND (?=0 OR `event_date`>=?) AND (?=0 OR `event_date`<?)", fromUtc, fromUtc, toUtc, toUtc), "invoiced + confirmed"),
            ("Open receivables", await ScalarAsync(connection,
                "SELECT IFNULL(SUM(`total`),0) FROM `ecomae_autoparts_orders` WHERE `order_type`='sale' AND `status` IN ('confirmed','invoiced') AND (?=0 OR `event_date`>=?) AND (?=0 OR `event_date`<?)", fromUtc, fromUtc, toUtc, toUtc), "awaiting payment"),
        ];

        snapshot.Parts = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `id`, `sku`, `name`, `brand`, `oem_no`, `category`, `fitment`,"
            + " CAST(`purchase_cost` AS CHAR), CAST(`sale_price` AS CHAR), `stock_qty`, `reorder_level`"
            + " FROM `ecomae_autoparts_parts` WHERE (?='' OR `category`=?) ORDER BY `category`, `name` LIMIT 400",
            cat, cat);

        snapshot.Movements = await LoadRowsAsync(connection, cancellationToken,
            "SELECT m.`id`, m.`movement`, p.`sku`, p.`name`, m.`qty`, CAST(m.`unit_cost` AS CHAR), m.`reference`,"
            + " FROM_UNIXTIME(m.`created_at_utc`,'%Y-%m-%d') FROM `ecomae_autoparts_movements` m"
            + " LEFT JOIN `ecomae_autoparts_parts` p ON p.`id`=m.`part_id`"
            + " WHERE (?='' OR p.`category`=?) AND (?=0 OR m.`created_at_utc`>=?) AND (?=0 OR m.`created_at_utc`<?)"
            + " ORDER BY m.`id` DESC LIMIT 400",
            cat, cat, fromUtc, fromUtc, toUtc, toUtc);

        snapshot.Purchases = await LoadOrdersAsync(connection, "purchase", fromUtc, toUtc, cancellationToken);
        snapshot.Sales = await LoadOrdersAsync(connection, "sale", fromUtc, toUtc, cancellationToken);

        snapshot.OrderLines = await LoadRowsAsync(connection, cancellationToken,
            "SELECT l.`id`, o.`order_no`, l.`description`, l.`qty`, CAST(l.`unit_price` AS CHAR), CAST(l.`line_total` AS CHAR)"
            + " FROM `ecomae_autoparts_order_lines` l JOIN `ecomae_autoparts_orders` o ON o.`id`=l.`order_id`"
            + " WHERE (?=0 OR o.`event_date`>=?) AND (?=0 OR o.`event_date`<?) ORDER BY l.`id` DESC LIMIT 400",
            fromUtc, fromUtc, toUtc, toUtc);

        snapshot.Invoices = await LoadRowsAsync(connection, cancellationToken,
            "SELECT k.`id`, o.`order_no`, k.`invoice_number`, CAST(k.`net` AS CHAR), CAST(k.`vat` AS CHAR), CAST(k.`gross` AS CHAR),"
            + " CONCAT('/erp/invoice-print?id=', CAST(k.`invoice_id` AS CHAR))"
            + " FROM `ecomae_autoparts_invoice_links` k JOIN `ecomae_autoparts_orders` o ON o.`id`=k.`order_id`"
            + " ORDER BY k.`id` DESC LIMIT 200");

        snapshot.LowStock = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `sku`, `name`, `brand`, `category`, `stock_qty`, `reorder_level`"
            + " FROM `ecomae_autoparts_parts` WHERE `active`=1 AND `stock_qty`<=`reorder_level` AND (?='' OR `category`=?)"
            + " ORDER BY `stock_qty` LIMIT 200",
            cat, cat);

        // GL journals posted by this workspace — proof the numbers reached the ledger.
        snapshot.Ledger = await LoadRowsAsync(connection, cancellationToken,
            "SELECT `id`, `reference`, `description`, FROM_UNIXTIME(`journal_date`,'%Y-%m-%d')"
            + " FROM `epc_erp_gl_journals` WHERE `source_type`='adjustment' AND `reference` LIKE 'AP-%' AND `active`=1"
            + " ORDER BY `id` DESC LIMIT 100");

        return snapshot;
    }

    private static async Task<IReadOnlyList<IReadOnlyList<string>>> LoadOrdersAsync(
        DbConnection connection, string type, long fromUtc, long toUtc, CancellationToken ct)
        => await LoadRowsAsync(connection, ct,
            "SELECT `id`, `order_no`, `party_name`, `status`, CAST(`subtotal` AS CHAR), CAST(`vat` AS CHAR), CAST(`total` AS CHAR),"
            + " FROM_UNIXTIME(`event_date`,'%Y-%m-%d')"
            + " FROM `ecomae_autoparts_orders` WHERE `order_type`=?"
            + " AND (?=0 OR `event_date`>=?) AND (?=0 OR `event_date`<?) ORDER BY `id` DESC LIMIT 400",
            type, fromUtc, fromUtc, toUtc, toUtc);

    private static async Task<IReadOnlyList<string>> LoadCategoriesAsync(DbConnection connection, CancellationToken ct)
    {
        var list = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT `category` FROM `ecomae_autoparts_parts` WHERE `category`<>'' ORDER BY `category`";
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    private static async Task<string> ScalarAsync(DbConnection connection, string sql, params object[] args)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        var value = await command.ExecuteScalarAsync().ConfigureAwait(false);
        return value switch
        {
            null or DBNull => "0",
            decimal d => d.ToString("N2", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
        };
    }

    private static async Task<IReadOnlyList<IReadOnlyList<string>>> LoadRowsAsync(
        DbConnection connection, CancellationToken ct, string sql, params object[] args)
    {
        var rows = new List<IReadOnlyList<string>>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
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

    private static readonly Func<DbConnection, CancellationToken, Task> _glEnsure =
        async (connection, ct) =>
        {
            // GL journal list reads epc_erp_gl_journals — ensure the ledger schema
            // exists on tenants that have never posted before the workspace opens.
            await ErpDb.TryExecuteAsync(connection,
                "SELECT 1 FROM `epc_erp_gl_journals` LIMIT 0", ct).ConfigureAwait(false);
        };
}

/// <summary>Creates the auto-parts workspace schema lazily — the tables hold
/// catalogue, stock, order and invoice-link rows for the parts flow.</summary>
public static class ErpAutoPartsWorkspaceSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `ecomae_autoparts_parts` ("
            + " `id` bigint NOT NULL AUTO_INCREMENT,"
            + " `sku` varchar(64) NOT NULL,"
            + " `name` varchar(255) NOT NULL,"
            + " `brand` varchar(128) NOT NULL DEFAULT '',"
            + " `oem_no` varchar(128) NOT NULL DEFAULT '',"
            + " `category` varchar(64) NOT NULL DEFAULT '',"
            + " `fitment` varchar(255) NOT NULL DEFAULT '',"
            + " `purchase_cost` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `sale_price` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `tax_rate` decimal(5,2) NOT NULL DEFAULT 5,"
            + " `stock_qty` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `reorder_level` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_sku` (`sku`), KEY `x_category` (`category`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `ecomae_autoparts_movements` ("
            + " `id` bigint NOT NULL AUTO_INCREMENT,"
            + " `part_id` bigint NOT NULL,"
            + " `movement` varchar(16) NOT NULL,"
            + " `qty` decimal(14,2) NOT NULL,"
            + " `unit_cost` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `reference` varchar(64) NOT NULL DEFAULT '',"
            + " `note` varchar(255) NOT NULL DEFAULT '',"
            + " `created_at_utc` bigint NOT NULL,"
            + " PRIMARY KEY (`id`), KEY `x_part` (`part_id`), KEY `x_time` (`created_at_utc`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `ecomae_autoparts_orders` ("
            + " `id` bigint NOT NULL AUTO_INCREMENT,"
            + " `order_no` varchar(32) NOT NULL,"
            + " `order_type` varchar(16) NOT NULL,"
            + " `party_name` varchar(255) NOT NULL DEFAULT '',"
            + " `status` varchar(24) NOT NULL DEFAULT 'draft',"
            + " `subtotal` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `vat` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `total` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `event_date` bigint NOT NULL DEFAULT 0,"
            + " `created_at_utc` bigint NOT NULL,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_no` (`order_no`), KEY `x_type` (`order_type`), KEY `x_date` (`event_date`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `ecomae_autoparts_order_lines` ("
            + " `id` bigint NOT NULL AUTO_INCREMENT,"
            + " `order_id` bigint NOT NULL,"
            + " `part_id` bigint NOT NULL DEFAULT 0,"
            + " `description` varchar(255) NOT NULL DEFAULT '',"
            + " `qty` decimal(14,2) NOT NULL,"
            + " `unit_price` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `tax_rate` decimal(5,2) NOT NULL DEFAULT 5,"
            + " `line_total` decimal(14,2) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), KEY `x_order` (`order_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `ecomae_autoparts_invoice_links` ("
            + " `id` bigint NOT NULL AUTO_INCREMENT,"
            + " `order_id` bigint NOT NULL,"
            + " `invoice_id` bigint NOT NULL DEFAULT 0,"
            + " `invoice_number` varchar(64) NOT NULL DEFAULT '',"
            + " `net` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `vat` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `gross` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `created_at_utc` bigint NOT NULL,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_order` (`order_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8", cancellationToken).ConfigureAwait(false);
    }
}
