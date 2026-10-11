using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rtl_ensure_schema</c> twin — verbatim DDL from
/// content/shop/finance/epc_erp_retail.php. PHP runs this at the top of the
/// retail tab and every rtl_* ajax case, so tenants provision lazily.
/// </summary>
internal static class ErpRtlSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_rtl_channel` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`channel_type` varchar(12) NOT NULL DEFAULT 'store'," +
            "`currency` varchar(8) NOT NULL DEFAULT ''," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_updated` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_company_code` (`company_id`,`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Retail channels'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_rtl_assortment` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`channel_id` int(11) NOT NULL DEFAULT 0," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_channel_item` (`channel_id`,`item_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Channel assortments'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_rtl_discount` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`channel_id` int(11) NOT NULL DEFAULT 0," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`disc_type` varchar(8) NOT NULL DEFAULT 'percent'," +
            "`value` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`starts` int(11) NOT NULL DEFAULT 0," +
            "`ends` int(11) NOT NULL DEFAULT 0," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_channel` (`channel_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Retail periodic discounts'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_rtl_txn` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`company_id` int(11) NOT NULL DEFAULT 0," +
            "`channel_id` int(11) NOT NULL DEFAULT 0," +
            "`receipt_no` varchar(40) NOT NULL DEFAULT ''," +
            "`gross` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`discount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`net` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`tax` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`total` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`tender_type` varchar(12) NOT NULL DEFAULT 'cash'," +
            "`txn_time` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_company` (`company_id`)," +
            "KEY `x_channel` (`channel_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS transactions'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_rtl_txn_line` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`txn_id` int(11) NOT NULL DEFAULT 0," +
            "`item_id` int(11) NOT NULL DEFAULT 0," +
            "`qty` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`unit_price` decimal(18,4) NOT NULL DEFAULT 0.0000," +
            "`line_discount` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`line_net` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_txn` (`txn_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS transaction lines'",
            cancellationToken).ConfigureAwait(false);
    }
}
