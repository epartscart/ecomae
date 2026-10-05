using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_ensure_schema</c> (expense part) twin — verbatim DDL for
/// <c>epc_hr_expenses</c> from content/shop/finance/epc_erp_hr.php. PHP creates
/// the table lazily, so a tenant without it gets the feature on first use.
/// </summary>
internal static class ErpHrExpenseSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_hr_expenses` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`employee_id` int(11) NOT NULL," +
            "`title` varchar(160) NOT NULL DEFAULT ''," +
            "`amount` decimal(16,2) NOT NULL DEFAULT 0.00," +
            "`status` varchar(12) NOT NULL DEFAULT 'draft'," +
            "`lines` mediumtext," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)," +
            "KEY `x_emp` (`employee_id`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Expense claims'",
            cancellationToken).ConfigureAwait(false);
    }
}
