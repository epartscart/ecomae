using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_sub_ensure_schema</c> twin — verbatim DDL from
/// content/shop/finance/epc_erp_subscriptions.php. PHP runs this at the top of
/// every subscriptions entry point, so tenants provision the tables lazily.
/// </summary>
internal static class ErpSubscriptionSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_subscriptions` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`customer` varchar(200) NOT NULL DEFAULT ''," +
            "`plan_name` varchar(160) NOT NULL DEFAULT ''," +
            "`amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`currency` varchar(3) NOT NULL DEFAULT 'AED'," +
            "`cycle` varchar(12) NOT NULL DEFAULT 'monthly'," +
            "`term_months` int(11) NOT NULL DEFAULT 12," +
            "`start_date` int(11) NOT NULL DEFAULT 0," +
            "`next_bill_date` int(11) NOT NULL DEFAULT 0," +
            "`status` varchar(12) NOT NULL DEFAULT 'active'," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "UNIQUE KEY `x_code` (`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Subscriptions'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_sub_invoices` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`subscription_id` int(11) NOT NULL," +
            "`period_start` int(11) NOT NULL DEFAULT 0," +
            "`period_end` int(11) NOT NULL DEFAULT 0," +
            "`amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`status` varchar(12) NOT NULL DEFAULT 'issued'," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_sub` (`subscription_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Subscription invoices'",
            cancellationToken).ConfigureAwait(false);
    }
}
