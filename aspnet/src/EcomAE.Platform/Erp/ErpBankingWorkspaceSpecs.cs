using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Cash-and-bank workspace tabs over epc_erp_cash_bank_accounts,
/// epc_erp_cash_bank_entries (direction: 1=in, 0=out) and
/// epc_erp_bank_statement_lines (matched_entry_id=0 means unreconciled).
/// </summary>
public static class ErpBankingWorkspaceSpecs
{
    private const string Accounts = "/erp/cash-accounts-app";
    private const string Entries = "/erp/cash-entries-app";

    private const string EntryJoin =
        " FROM `epc_erp_cash_bank_entries` e" +
        " LEFT JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = e.`account_id`";

    private const string EntryCols =
        "SELECT IFNULL(a.`name`,'(none)'), e.`entry_type`, IF(e.`direction`=1,'in','out')," +
        " e.`amount`, IFNULL(e.`reference`,''), FROM_UNIXTIME(e.`time`)";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["cb_change_history"] = new(
                ["Account", "Type", "Dir", "Amount", "Reference", "When"],
                EntryCols + EntryJoin +
                " WHERE IFNULL(e.`active`,1) = 1 ORDER BY e.`time` DESC, e.`id` DESC LIMIT 400",
                "No bank transactions yet — post a receipt or payment.",
                Entries),

            ["cb_bridged_tx"] = new(
                ["Account", "Type", "Dir", "Amount", "Reference", "When"],
                EntryCols + EntryJoin +
                " WHERE IFNULL(e.`active`,1) = 1 AND e.`entry_type` LIKE '%transfer%'" +
                " ORDER BY e.`time` DESC LIMIT 300",
                "No transfers yet — move cash between accounts to see them.",
                Entries),

            ["cb_print_statement"] = new(
                ["Account", "Date", "Description", "Reference", "Amount", "Matched"],
                "SELECT l.`account_id`, FROM_UNIXTIME(l.`line_date`)," +
                " LEFT(IFNULL(l.`description`,''),80), IFNULL(l.`reference`,''), l.`amount`," +
                " IF(l.`matched_entry_id` > 0,'yes','no')" +
                " FROM `epc_erp_bank_statement_lines` l" +
                " ORDER BY l.`line_date` DESC, l.`id` DESC LIMIT 400",
                "No statement lines — import a bank statement first.",
                Accounts),

            ["cb_unreconciled"] = new(
                ["Account", "Date", "Description", "Reference", "Amount"],
                "SELECT l.`account_id`, FROM_UNIXTIME(l.`line_date`)," +
                " LEFT(IFNULL(l.`description`,''),80), IFNULL(l.`reference`,''), l.`amount`" +
                " FROM `epc_erp_bank_statement_lines` l" +
                " WHERE l.`matched_entry_id` = 0" +
                " ORDER BY l.`line_date` DESC LIMIT 300",
                "Nothing unreconciled.",
                Accounts),

            ["cb_account_recon_report"] = new(
                ["Account", "Statement lines", "Matched", "Unmatched", "Statement total"],
                "SELECT l.`account_id`, COUNT(l.`id`)," +
                " SUM(l.`matched_entry_id` > 0), SUM(l.`matched_entry_id` = 0)," +
                " IFNULL(SUM(l.`amount`),0)" +
                " FROM `epc_erp_bank_statement_lines` l" +
                " GROUP BY l.`account_id` ORDER BY 4 DESC LIMIT 200",
                "No statements to reconcile.",
                Accounts),

            ["cb_bank_to_ledger"] = new(
                ["Account", "Entries", "In", "Out", "Net"],
                "SELECT a.`name`, COUNT(e.`id`)," +
                " IFNULL(SUM(IF(e.`direction`=1,e.`amount`,0)),0)," +
                " IFNULL(SUM(IF(e.`direction`=0,e.`amount`,0)),0)," +
                " IFNULL(SUM(IF(e.`direction`=1,e.`amount`,-e.`amount`)),0)" +
                EntryJoin +
                " WHERE IFNULL(e.`active`,1) = 1 GROUP BY a.`id` ORDER BY 5 DESC LIMIT 200",
                "No cash-bank activity yet.",
                Accounts),

            ["cb_balance_control"] = new(
                ["Account", "Type", "Currency", "Opening", "Movements", "Balance"],
                "SELECT a.`name`, a.`account_type`, IFNULL(a.`currency_code`,'AED')," +
                " a.`opening_balance`," +
                " IFNULL((SELECT SUM(IF(e.`direction`=1,e.`amount`,-e.`amount`))" +
                "  FROM `epc_erp_cash_bank_entries` e WHERE e.`account_id` = a.`id`" +
                "  AND IFNULL(e.`active`,1) = 1),0)," +
                " a.`opening_balance` +" +
                " IFNULL((SELECT SUM(IF(e.`direction`=1,e.`amount`,-e.`amount`))" +
                "  FROM `epc_erp_cash_bank_entries` e WHERE e.`account_id` = a.`id`" +
                "  AND IFNULL(e.`active`,1) = 1),0)" +
                " FROM `epc_erp_cash_bank_accounts` a WHERE a.`active` = 1" +
                " ORDER BY a.`name` LIMIT 200",
                "No bank accounts — create one in Cash accounts.",
                Accounts),

            ["cb_deposit_by_date"] = new(
                ["Date", "Deposits", "Total"],
                "SELECT DATE_FORMAT(FROM_UNIXTIME(e.`time`),'%Y-%m-%d')," +
                " COUNT(e.`id`), IFNULL(SUM(e.`amount`),0)" +
                EntryJoin +
                " WHERE IFNULL(e.`active`,1) = 1 AND e.`direction` = 1" +
                " GROUP BY 1 ORDER BY 1 DESC LIMIT 120",
                "No deposits yet.",
                Entries),

            ["cb_deposit_report"] = new(
                ["Account", "Type", "Amount", "Reference", "When"],
                "SELECT IFNULL(a.`name`,'(none)'), e.`entry_type`, e.`amount`," +
                " IFNULL(e.`reference`,''), FROM_UNIXTIME(e.`time`)" +
                EntryJoin +
                " WHERE IFNULL(e.`active`,1) = 1 AND e.`direction` = 1" +
                " ORDER BY e.`time` DESC LIMIT 300",
                "No deposits yet.",
                Entries),

            ["cb_cash_forecast_auto"] = new(
                ["Account", "Balance", "In (30d)", "Out (30d)", "Projected"],
                "SELECT a.`name`," +
                " a.`opening_balance` +" +
                " IFNULL((SELECT SUM(IF(e.`direction`=1,e.`amount`,-e.`amount`))" +
                "  FROM `epc_erp_cash_bank_entries` e WHERE e.`account_id` = a.`id`" +
                "  AND IFNULL(e.`active`,1) = 1),0)," +
                " IFNULL((SELECT SUM(e.`amount`) FROM `epc_erp_cash_bank_entries` e" +
                "  WHERE e.`account_id` = a.`id` AND e.`direction` = 1" +
                "  AND e.`time` > UNIX_TIMESTAMP() - 2592000 AND IFNULL(e.`active`,1) = 1),0)," +
                " IFNULL((SELECT SUM(e.`amount`) FROM `epc_erp_cash_bank_entries` e" +
                "  WHERE e.`account_id` = a.`id` AND e.`direction` = 0" +
                "  AND e.`time` > UNIX_TIMESTAMP() - 2592000 AND IFNULL(e.`active`,1) = 1),0)," +
                " a.`opening_balance` +" +
                " IFNULL((SELECT SUM(IF(e.`direction`=1,e.`amount`,-e.`amount`))" +
                "  FROM `epc_erp_cash_bank_entries` e WHERE e.`account_id` = a.`id`" +
                "  AND IFNULL(e.`active`,1) = 1),0)" +
                " FROM `epc_erp_cash_bank_accounts` a WHERE a.`active` = 1" +
                " ORDER BY a.`name` LIMIT 200",
                "No accounts to forecast.",
                Accounts),

            ["cb_bank_groups"] = new(
                ["Type", "Accounts", "Total balance"],
                "SELECT a.`account_type`, COUNT(a.`id`)," +
                " IFNULL(SUM(a.`opening_balance`),0)" +
                " FROM `epc_erp_cash_bank_accounts` a WHERE a.`active` = 1" +
                " GROUP BY a.`account_type` ORDER BY 3 DESC LIMIT 100",
                "No account groups.",
                Accounts),

            ["cb_check_stats"] = new(
                ["Entry type", "Lines", "Total", "Last"],
                "SELECT e.`entry_type`, COUNT(e.`id`), IFNULL(SUM(e.`amount`),0)," +
                " FROM_UNIXTIME(IFNULL(MAX(e.`time`),0))" +
                EntryJoin +
                " WHERE IFNULL(e.`active`,1) = 1 GROUP BY e.`entry_type` ORDER BY 3 DESC LIMIT 100",
                "No cheque activity.",
                Entries),
        };

    /// <summary>Lazy-creates the banking tables the specs read.</summary>
    internal static async Task EnsureBankingAsync(DbConnection c, CancellationToken ct)
    {
        await ErpCashWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `name` varchar(120) NOT NULL DEFAULT '', `account_type` varchar(24) NOT NULL DEFAULT 'bank'," +
            " `bank_name` varchar(120) NOT NULL DEFAULT '', `account_number` varchar(64) NOT NULL DEFAULT ''," +
            " `currency_code` varchar(3) NOT NULL DEFAULT 'AED', `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00," +
            " `office_id` int(11) NOT NULL DEFAULT 0, `active` tinyint(1) NOT NULL DEFAULT 1," +
            " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)," +
            " KEY `x_active` (`active`), KEY `x_office` (`office_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct)
            .ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_bank_statement_lines` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `account_id` int(11) NOT NULL, `line_date` int(11) NOT NULL," +
            " `description` varchar(255) DEFAULT NULL, `reference` varchar(128) DEFAULT NULL," +
            " `amount` decimal(14,2) NOT NULL DEFAULT 0.00, `direction` tinyint(1) NOT NULL," +
            " `matched_entry_id` int(11) NOT NULL DEFAULT 0, `import_batch` varchar(64) DEFAULT NULL," +
            " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)," +
            " KEY `x_account` (`account_id`,`matched_entry_id`), KEY `x_date` (`line_date`))" +
            " ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);
    }
}
