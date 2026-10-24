using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_credit_ensure_schema</c> / <c>epc_coll_ensure_schema</c> twin —
/// verbatim DDL from content/shop/finance/epc_erp_credit.php and
/// epc_erp_collections.php. PHP runs these at the top of the credit, collections
/// and credit-limit entry points, so fresh tenants provision lazily.
/// </summary>
internal static class ErpCreditSchema
{
    private static readonly (string Column, string Definition)[] MasterColumns =
    {
        ("customer_account", "varchar(32) DEFAULT NULL"),
        ("customer_name", "varchar(255) DEFAULT NULL"),
        ("customer_group", "varchar(64) DEFAULT NULL"),
        ("legal_entity_id", "int(11) NOT NULL DEFAULT 0"),
        ("business_unit_id", "int(11) NOT NULL DEFAULT 0"),
        ("currency_code", "varchar(8) DEFAULT NULL"),
        ("payment_method", "varchar(32) DEFAULT NULL"),
        ("delivery_terms", "varchar(32) DEFAULT NULL"),
        ("delivery_mode", "varchar(32) DEFAULT NULL"),
        ("trn", "varchar(64) DEFAULT NULL"),
        ("tax_exempt", "tinyint(1) NOT NULL DEFAULT 0"),
        ("sales_tax_group", "varchar(64) DEFAULT NULL"),
        ("contact_person", "varchar(255) DEFAULT NULL"),
        ("contact_email", "varchar(128) DEFAULT NULL"),
        ("contact_phone", "varchar(64) DEFAULT NULL"),
        ("website", "varchar(255) DEFAULT NULL"),
        ("address", "varchar(512) DEFAULT NULL"),
        ("city", "varchar(128) DEFAULT NULL"),
        ("state_region", "varchar(128) DEFAULT NULL"),
        ("postal_code", "varchar(32) DEFAULT NULL"),
        ("country_code", "varchar(8) DEFAULT NULL"),
    };

    /// <summary>epc_credit_profiles plus the ecom customer-master columns.</summary>
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_credit_profiles` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`customer_id` int(11) NOT NULL," +
            "`credit_limit` decimal(14,2) NOT NULL DEFAULT 0.00," +
            "`terms_days` int(11) NOT NULL DEFAULT 30," +
            "`on_hold` tinyint(1) NOT NULL DEFAULT 0," +
            "`risk_band` varchar(16) NOT NULL DEFAULT 'normal'," +
            "`notes` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_customer` (`customer_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customer credit profiles'",
            cancellationToken).ConfigureAwait(false);

        var existing = await LoadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var (column, definition) in MasterColumns)
        {
            if (existing.Contains(column))
            {
                continue;
            }

            await ErpDb.TryExecuteAsync(connection,
                "ALTER TABLE `epc_credit_profiles` ADD COLUMN `" + column + "` " + definition,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>epc_coll_cases / epc_coll_activity / epc_coll_dunning / epc_coll_hold (after credit profiles).</summary>
    public static async Task EnsureCollectionsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_coll_cases` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`status` varchar(20) NOT NULL DEFAULT 'new'," +
            "`balance` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`promise_amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`promise_date` int(11) NOT NULL DEFAULT 0," +
            "`assigned_to` varchar(120) NOT NULL DEFAULT ''," +
            "`notes` text," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_customer` (`customer_id`)," +
            "KEY `x_status` (`status`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Collection cases'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_coll_activity` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`case_id` int(11) NOT NULL DEFAULT 0," +
            "`type` varchar(20) NOT NULL DEFAULT 'note'," +
            "`outcome` varchar(200) NOT NULL DEFAULT ''," +
            "`amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`follow_up_date` int(11) NOT NULL DEFAULT 0," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_case` (`case_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Collection activities'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_coll_dunning` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`run_id` int(11) NOT NULL DEFAULT 0," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`level` int(11) NOT NULL DEFAULT 0," +
            "`amount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`message` varchar(255) NOT NULL DEFAULT ''," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_run` (`run_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Dunning run log'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_coll_hold` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`action` varchar(10) NOT NULL DEFAULT 'place'," +
            "`reason` varchar(255) NOT NULL DEFAULT ''," +
            "`actor` varchar(120) NOT NULL DEFAULT ''," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_customer` (`customer_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Credit hold action log'",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HashSet<string>> LoadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `COLUMN_NAME` FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?");
            ErpDb.AddParameters(cmd, "epc_credit_profiles");
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    var name = reader.GetValue(0)?.ToString() ?? "";
                    if (name.Length > 0)
                    {
                        set.Add(name);
                    }
                }
            }
        }
        catch (DbException)
        {
        }

        return set;
    }
}
