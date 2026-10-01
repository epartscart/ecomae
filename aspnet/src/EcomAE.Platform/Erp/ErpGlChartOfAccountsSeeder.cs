using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Port of PHP <c>epc_erp_gl_ensure_schema</c> → <c>epc_erp_gl_seed_coa</c> / <c>epc_erp_gl_link_cash_coa</c>:
/// a tenant with an empty chart of accounts receives the system accounts on first GL use,
/// and active cash/bank accounts are linked to 1000/1010.
/// </summary>
public static class ErpGlChartOfAccountsSeeder
{
    public static readonly IReadOnlyList<(string Code, string Name, string Type, string Side, string Description)> SystemAccounts =
    [
        ("1000", "Cash on hand", "asset", "debit", "Petty cash and cash drawers"),
        ("1010", "Bank", "asset", "debit", "Bank current accounts"),
        ("1100", "Accounts receivable", "asset", "debit", "Customer trade debtors"),
        ("1150", "VAT input (recoverable)", "asset", "debit", "UAE VAT 5% on purchases"),
        ("2000", "Accounts payable", "liability", "credit", "Supplier trade creditors"),
        ("2100", "VAT output (payable)", "liability", "credit", "UAE VAT 5% on sales"),
        ("3000", "Owner's equity", "equity", "credit", "Capital / owner funds"),
        ("3100", "Retained earnings", "equity", "credit", "Accumulated profit"),
        ("4000", "Sales revenue", "revenue", "credit", "Parts sales ex VAT"),
        ("5000", "Cost of goods sold", "expense", "debit", "Purchase cost of parts sold"),
        ("6100", "General expenses", "expense", "debit", "Operating expenses"),
    ];

    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_coa_accounts` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `code` varchar(16) NOT NULL,"
            + " `name` varchar(255) NOT NULL,"
            + " `account_type` enum('asset','liability','equity','revenue','expense') NOT NULL,"
            + " `normal_side` enum('debit','credit') NOT NULL DEFAULT 'debit',"
            + " `parent_id` int(11) NOT NULL DEFAULT 0,"
            + " `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `description` varchar(512) DEFAULT NULL,"
            + " `system_flag` tinyint(1) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_code` (`code`),"
            + " KEY `x_type` (`account_type`,`active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP chart of accounts'",
            cancellationToken).ConfigureAwait(false);

        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_coa_accounts`"),
                cancellationToken).ConfigureAwait(false);

            if (count <= 0)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var row in SystemAccounts)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional(
                            "INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`)"
                            + " VALUES (?, ?, ?, ?, ?, 1, ?)"),
                        cancellationToken,
                        row.Code,
                        row.Name,
                        row.Type,
                        row.Side,
                        row.Description,
                        now).ConfigureAwait(false);
                }
            }

            await LinkCashAccountsAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Best-effort like PHP: GL stays optional when the COA cannot be installed.
        }
    }

    private static async Task LinkCashAccountsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var cash = await CoaIdAsync(connection, "1000", cancellationToken).ConfigureAwait(false);
        var bank = await CoaIdAsync(connection, "1010", cancellationToken).ConfigureAwait(false);
        if (cash <= 0 || bank <= 0)
        {
            return;
        }

        await ErpDb.TryExecuteAsync(
            connection,
            "UPDATE `epc_erp_cash_bank_accounts` SET `coa_id` = CASE WHEN `account_type` = 'bank' THEN "
            + bank.ToString(CultureInfo.InvariantCulture) + " ELSE "
            + cash.ToString(CultureInfo.InvariantCulture) + " END WHERE `active` = 1 AND `coa_id` = 0",
            cancellationToken).ConfigureAwait(false);
    }

    private static Task<long> CoaIdAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code);
}
