using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// ecom F&O general-ledger foundation, tenant-generic:
/// financial dimension types (legal entity, business unit, cost centre + tenant-defined),
/// dimension values, dimension sets (ordered compositions like "MA>BU>CC"), fiscal
/// calendars with year/period structure, exchange-rate pairs with dated rates, bank
/// accounts with reconciliation status, and the extra GL-journal columns that carry
/// dimensioned account strings (acct-BU-CC-LE). All schema evolves lazily per tenant DB.
/// </summary>
public interface IErpD365GlService
{
    Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveDimensionTypeAsync(DbConnection connection, long id, string code, string name, string description, int adminId, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SaveDimensionValueAsync(DbConnection connection, long typeId, string valueCode, string valueName, string parentValue, int adminId, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, string?>>> DimensionTypesAsync(DbConnection connection, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, string?>>> DimensionValuesAsync(DbConnection connection, long typeId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveDimensionSetAsync(DbConnection connection, long id, string code, string name, string composition, int adminId, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, string?>>> DimensionSetsAsync(DbConnection connection, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveFiscalCalendarAsync(DbConnection connection, long id, string code, string name, int year, string periodType, string startDate, string endDate, string status, int adminId, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, string?>>> FiscalCalendarsAsync(DbConnection connection, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveExchangeRateAsync(DbConnection connection, string fromCurrency, string toCurrency, decimal rate, string rateDate, string rateType, int adminId, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, string?>>> ExchangeRatesAsync(DbConnection connection, string fromCurrency, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveBankAccountAsync(DbConnection connection, long id, IReadOnlyDictionary<string, string?> fields, int adminId, CancellationToken cancellationToken = default);
    Task<List<Dictionary<string, string?>>> BankAccountsAsync(DbConnection connection, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SetBankReconciledAsync(DbConnection connection, long entryId, bool reconciled, int adminId, CancellationToken cancellationToken = default);

    /// <summary>Attach ecom dimension columns onto epc_erp_gl_lines + journal metadata columns.</summary>
    Task<ErpSimpleWriteResult> SaveJournalLineDimsAsync(DbConnection connection, long lineId, IReadOnlyDictionary<string, string?> fields, int adminId, CancellationToken cancellationToken = default);

    /// <summary>Trial balance pivoted by a dimension set: rows = set members, col pairs = debit/credit totals.</summary>
    Task<List<Dictionary<string, string?>>> TrialBalanceBySetAsync(DbConnection connection, long setId, CancellationToken cancellationToken = default);

    /// <summary>MainAccountDetails parity columns on epc_erp_coa_accounts.</summary>
    Task<ErpSimpleWriteResult> SaveCoaFieldsAsync(DbConnection connection, long accountId, IReadOnlyDictionary<string, string?> fields, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpD365GlService : IErpD365GlService
{
    /// <summary>Dimension type system codes — tenants can add their own too.</summary>
    public static readonly (string Code, string Name)[] BuiltinDimensionTypes =
    [
        ("legal_entity", "Legal entity"),
        ("business_unit", "Business unit"),
        ("cost_centre", "Cost centre"),
        ("department", "Department"),
        ("reporting_purpose", "Reporting purpose"),
        ("worker", "Worker"),
        ("warehouse", "Warehouse"),
        ("project", "Project"),
    ];

    /// <summary>BankAccountTable fields (epc_erp_bank_accounts).</summary>
    public static readonly string[] BankAccountColumns =
    [
        "bank_account", "name", "bank_group", "routing_number", "bank_account_number",
        "iban", "main_account", "currency_code", "cin", "company_id", "business_unit_id",
        "status", "opening_balance", "current_balance",
    ];

    /// <summary>MainAccountDetails parity columns (epc_erp_coa_accounts).</summary>
    public static readonly string[] CoaColumns =
    [
        "main_account_type", "main_account_category", "debit_credit_default",
        "debit_credit_proposal", "db_cr_requirement", "balances_control",
        "validate_sales_tax", "validate_currency", "validate_posting_type",
        "posting_type", "level_limit", "total_level", "exchange_rate_type",
        "currency_code", "financial_reporting_exchange_rate_type",
        "consolidation_account_group", "default_offset_account", "locked_for_journal",
        "company_id", "business_unit_id", "cost_centre_id", "reporting_purpose",
        "default_dimension_string", "active",
    ];

    /// <summary>Journal-line dimension columns (epc_erp_gl_lines).</summary>
    public static readonly string[] JournalLineDimColumns =
    [
        "cost_centre_id", "dimension_type_code", "dimension_value_code",
        "dimension_string", "account_type", "offset_account_type",
        "offset_account", "voucher", "voucher_date", "method_of_payment",
        "deposit_slip", "maturity_date", "currency_code", "amount_currency",
    ];

    public async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_gl_dim_types` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `code` varchar(32) NOT NULL,"
            + " `name` varchar(120) NOT NULL DEFAULT '',"
            + " `description` varchar(255) NOT NULL DEFAULT '',"
            + " `is_system` tinyint(1) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_code` (`code`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ecom financial dimension types'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_gl_dim_values` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `type_id` int(11) NOT NULL,"
            + " `value_code` varchar(64) NOT NULL,"
            + " `value_name` varchar(255) NOT NULL DEFAULT '',"
            + " `parent_value` varchar(64) NOT NULL DEFAULT '',"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_type_val` (`type_id`,`value_code`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ecom financial dimension values'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_gl_dim_sets` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `code` varchar(32) NOT NULL,"
            + " `name` varchar(120) NOT NULL DEFAULT '',"
            + " `composition` varchar(255) NOT NULL DEFAULT ''," // e.g. "main_account>business_unit>cost_centre"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_code` (`code`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ecom financial dimension sets'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fiscal_calendars` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `code` varchar(32) NOT NULL,"
            + " `name` varchar(120) NOT NULL DEFAULT '',"
            + " `year` int(11) NOT NULL DEFAULT 0,"
            + " `period_type` enum('opening','operating','closing') NOT NULL DEFAULT 'operating',"
            + " `start_date` int(11) NOT NULL DEFAULT 0,"
            + " `end_date` int(11) NOT NULL DEFAULT 0,"
            + " `status` enum('open','closed','permanently_closed') NOT NULL DEFAULT 'open',"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_cal_year` (`code`,`year`,`period_type`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ecom fiscal calendars'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_fx_rates` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `from_currency` varchar(8) NOT NULL,"
            + " `to_currency` varchar(8) NOT NULL,"
            + " `rate` decimal(16,8) NOT NULL DEFAULT 1.00000000,"
            + " `rate_date` int(11) NOT NULL DEFAULT 0,"
            + " `rate_type` varchar(32) NOT NULL DEFAULT 'Default',"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_pair_date` (`from_currency`,`to_currency`,`rate_date`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ecom currency exchange rates'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_bank_accounts` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `bank_account` varchar(24) NOT NULL DEFAULT '',"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_bank_account` (`bank_account`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ecom bank accounts'",
            cancellationToken).ConfigureAwait(false);
        foreach (var col in BankAccountColumns)
        {
            var def = col is "opening_balance" or "current_balance"
                ? "decimal(16,2) NOT NULL DEFAULT 0.00"
                : col is "company_id" or "business_unit_id" ? "int(11) NOT NULL DEFAULT 0"
                : col == "status" ? "varchar(24) NOT NULL DEFAULT 'active'"
                : "varchar(255) NOT NULL DEFAULT ''";
            await AddCol(connection, "epc_erp_bank_accounts", col, def, cancellationToken).ConfigureAwait(false);
        }
        await AddCol(connection, "epc_erp_bank_accounts", "active", "tinyint(1) NOT NULL DEFAULT 1", cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_bank_recon` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `bank_account_id` int(11) NOT NULL,"
            + " `entry_id` int(11) NOT NULL," // epc_erp_cash_bank_entries.id
            + " `reconciled` tinyint(1) NOT NULL DEFAULT 0,"
            + " `reconciled_at` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `x_entry` (`entry_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bank reconciliation marks'",
            cancellationToken).ConfigureAwait(false);

        await AddCol(connection, "epc_erp_cash_bank_entries", "bank_account_id", "int(11) NOT NULL DEFAULT 0", cancellationToken).ConfigureAwait(false);

        // GL line dimension columns
        foreach (var col in JournalLineDimColumns)
        {
            var def = col is "cost_centre_id" ? "int(11) NOT NULL DEFAULT 0"
                : col is "amount_currency" ? "decimal(14,2) NOT NULL DEFAULT 0.00"
                : col is "voucher_date" or "maturity_date" ? "int(11) NOT NULL DEFAULT 0"
                : "varchar(255) NOT NULL DEFAULT ''";
            await AddCol(connection, "epc_erp_gl_lines", col, def, cancellationToken).ConfigureAwait(false);
        }

        // COA parity columns
        foreach (var col in CoaColumns)
        {
            var def = col is "company_id" or "business_unit_id" or "cost_centre_id" or "level_limit" or "total_level" or "locked_for_journal" or "active"
                ? "int(11) NOT NULL DEFAULT 0"
                : "varchar(255) NOT NULL DEFAULT ''";
            await AddCol(connection, "epc_erp_coa_accounts", col, def, cancellationToken).ConfigureAwait(false);
        }

        // Seed the builtin dimension types if the table is empty.
        var haveTypes = await ErpDb.LongAsync(connection, null, "SELECT COUNT(1) FROM `epc_erp_gl_dim_types`", cancellationToken).ConfigureAwait(false);
        if (haveTypes == 0)
        {
            foreach (var (code, name) in BuiltinDimensionTypes)
            {
                await ErpDb.ExecuteAsync(connection, null,
                    "INSERT IGNORE INTO `epc_erp_gl_dim_types` (`code`,`name`,`is_system`,`time_created`) VALUES (?,?,1,?)",
                    cancellationToken, code, name, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            }
        }
    }

    private static async Task AddCol(DbConnection c, string table, string column, string def, CancellationToken t)
        => await ErpDb.TryExecuteAsync(c, "ALTER TABLE `" + table + "` ADD `" + column + "` " + def, t).ConfigureAwait(false);

    public async Task<ErpSimpleWriteResult> SaveDimensionTypeAsync(DbConnection c, long id, string code, string name, string description, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        code = code.Trim().ToLowerInvariant();
        if (code.Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Pass dimension type code.");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(c, null, "UPDATE `epc_erp_gl_dim_types` SET `name`=?,`description`=?,`time_updated`=? WHERE `id`=?", t, name, description, now, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dimension type updated.", id);
        }
        await ErpDb.ExecuteAsync(c, null, "INSERT INTO `epc_erp_gl_dim_types` (`code`,`name`,`description`,`admin_id`,`time_created`) VALUES (?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`description`=VALUES(`description`)", t, code, name, description, adminId, now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Dimension type " + code + " saved.", id);
    }

    public async Task<ErpSimpleWriteResult> SaveDimensionValueAsync(DbConnection c, long typeId, string valueCode, string valueName, string parentValue, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        if (typeId <= 0 || valueCode.Trim().Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Pass type_id + value_code.");
        await ErpDb.ExecuteAsync(c, null, "INSERT INTO `epc_erp_gl_dim_values` (`type_id`,`value_code`,`value_name`,`parent_value`,`admin_id`,`time_created`) VALUES (?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `value_name`=VALUES(`value_name`),`parent_value`=VALUES(`parent_value`)",
            t, typeId, valueCode.Trim(), valueName, parentValue, adminId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Dimension value " + valueCode + " saved.", typeId);
    }

    public async Task<List<Dictionary<string, string?>>> DimensionTypesAsync(DbConnection c, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        return await ErpD365DocCycleService.RowsAsync(c, "SELECT * FROM `epc_erp_gl_dim_types` ORDER BY `code`", t).ConfigureAwait(false);
    }

    public async Task<List<Dictionary<string, string?>>> DimensionValuesAsync(DbConnection c, long typeId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        return await ErpD365DocCycleService.RowsAsync(c, "SELECT * FROM `epc_erp_gl_dim_values` WHERE `type_id` = ? ORDER BY `value_code`", t, typeId).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveDimensionSetAsync(DbConnection c, long id, string code, string name, string composition, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        if (code.Trim().Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Pass set code.");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(c, null, "UPDATE `epc_erp_gl_dim_sets` SET `name`=?,`composition`=?,`time_updated`=? WHERE `id`=?", t, name, composition, now, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dimension set updated.", id);
        }
        await ErpDb.ExecuteAsync(c, null, "INSERT INTO `epc_erp_gl_dim_sets` (`code`,`name`,`composition`,`admin_id`,`time_created`) VALUES (?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`composition`=VALUES(`composition`)",
            t, code.Trim(), name, composition, adminId, now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Dimension set " + code + " saved.", id);
    }

    public async Task<List<Dictionary<string, string?>>> DimensionSetsAsync(DbConnection c, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        return await ErpD365DocCycleService.RowsAsync(c, "SELECT * FROM `epc_erp_gl_dim_sets` ORDER BY `code`", t).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveFiscalCalendarAsync(DbConnection c, long id, string code, string name, int year, string periodType, string startDate, string endDate, string status, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        if (code.Trim().Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Pass calendar code.");
        var pt = periodType.ToLowerInvariant() is "opening" or "closing" ? periodType.ToLowerInvariant() : "operating";
        var st = status.ToLowerInvariant() is "closed" or "permanently_closed" ? status.ToLowerInvariant() : "open";
        var sd = DateToUnix(startDate); var ed = DateToUnix(endDate);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(c, null, "UPDATE `epc_erp_fiscal_calendars` SET `name`=?,`year`=?,`period_type`=?,`start_date`=?,`end_date`=?,`status`=? WHERE `id`=?", t, name, year, pt, sd, ed, st, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fiscal calendar updated.", id);
        }
        await ErpDb.ExecuteAsync(c, null, "INSERT INTO `epc_erp_fiscal_calendars` (`code`,`name`,`year`,`period_type`,`start_date`,`end_date`,`status`,`admin_id`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`status`=VALUES(`status`)",
            t, code.Trim(), name, year, pt, sd, ed, st, adminId, now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fiscal calendar " + code + " " + year + " saved.", id);
    }

    private static long DateToUnix(string s)
        => DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? d.ToUnixTimeSeconds()
            : long.TryParse(s, out var u) ? u : 0;

    public async Task<List<Dictionary<string, string?>>> FiscalCalendarsAsync(DbConnection c, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        return await ErpD365DocCycleService.RowsAsync(c, "SELECT * FROM `epc_erp_fiscal_calendars` ORDER BY `code`,`year`,`period_type`", t).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveExchangeRateAsync(DbConnection c, string fromCurrency, string toCurrency, decimal rate, string rateDate, string rateType, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        if (fromCurrency.Trim().Length == 0 || toCurrency.Trim().Length == 0 || rate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Pass from_currency, to_currency and a positive rate.");
        }
        await ErpDb.ExecuteAsync(c, null,
            "INSERT INTO `epc_erp_fx_rates` (`from_currency`,`to_currency`,`rate`,`rate_date`,`rate_type`,`admin_id`,`time_created`) VALUES (?,?,?,?,?,?,?)",
            t, fromCurrency.Trim().ToUpperInvariant(), toCurrency.Trim().ToUpperInvariant(), rate, DateToUnix(rateDate), rateType.Length > 0 ? rateType : "Default", adminId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Rate " + fromCurrency + "/" + toCurrency + " = " + rate.ToString(CultureInfo.InvariantCulture) + " saved.", 0);
    }

    public async Task<List<Dictionary<string, string?>>> ExchangeRatesAsync(DbConnection c, string fromCurrency, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        var sql = "SELECT * FROM `epc_erp_fx_rates`" + (fromCurrency.Length > 0 ? " WHERE `from_currency` = ?" : "") + " ORDER BY `rate_date` DESC, `id` DESC LIMIT 500";
        return fromCurrency.Length > 0
            ? await ErpD365DocCycleService.RowsAsync(c, sql, t, fromCurrency.Trim().ToUpperInvariant()).ConfigureAwait(false)
            : await ErpD365DocCycleService.RowsAsync(c, sql, t).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveBankAccountAsync(DbConnection c, long id, IReadOnlyDictionary<string, string?> f, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var set = new StringBuilder();
        var args = new List<object?>();
        foreach (var col in BankAccountColumns)
        {
            if (!f.TryGetValue(col, out var v)) continue;
            if (set.Length > 0) set.Append(", ");
            set.Append('`').Append(col).Append("` = ?");
            args.Add(v);
        }
        if (id > 0)
        {
            if (set.Length == 0) return ErpSimpleWriteResult.Ok("Nothing to update.", id);
            args.Add(now); args.Add(id);
            await ErpDb.ExecuteAsync(c, null, "UPDATE `epc_erp_bank_accounts` SET " + set + ", `time_updated` = ? WHERE `id` = ?", t, args.ToArray()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Bank account updated.", id);
        }
        var acct = f.TryGetValue("bank_account", out var ba) ? ba ?? "" : "";
        if (acct.Length == 0) return ErpSimpleWriteResult.Fail("invalid", "Pass bank_account code.");
        var cols = new List<string> { "`admin_id`", "`time_created`", "`time_updated`" };
        var vals = new List<object?> { adminId, now, now };
        foreach (var col in BankAccountColumns)
        {
            if (!f.TryGetValue(col, out var v)) continue;
            cols.Add("`" + col + "`"); vals.Add(v);
        }
        await ErpDb.ExecuteAsync(c, null,
            "INSERT INTO `epc_erp_bank_accounts` (" + string.Join(",", cols) + ") VALUES (" + string.Join(",", vals.Select(_ => "?")) + ") ON DUPLICATE KEY UPDATE `time_updated` = VALUES(`time_updated`)",
            t, vals.ToArray()).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Bank account " + acct + " saved.", id);
    }

    public async Task<List<Dictionary<string, string?>>> BankAccountsAsync(DbConnection c, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        return await ErpD365DocCycleService.RowsAsync(c, "SELECT * FROM `epc_erp_bank_accounts` ORDER BY `bank_account`", t).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SetBankReconciledAsync(DbConnection c, long entryId, bool reconciled, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        if (entryId <= 0) return ErpSimpleWriteResult.Fail("invalid", "Pass entry_id.");
        await ErpDb.ExecuteAsync(c, null,
            "INSERT INTO `epc_erp_bank_recon` (`bank_account_id`,`entry_id`,`reconciled`,`reconciled_at`,`admin_id`,`time_created`)"
            + " SELECT `bank_account_id`, ?, ?, ?, ?, ? FROM `epc_erp_cash_bank_entries` WHERE `id` = ?"
            + " ON DUPLICATE KEY UPDATE `reconciled` = VALUES(`reconciled`), `reconciled_at` = VALUES(`reconciled_at`)",
            t, entryId, reconciled ? 1 : 0, reconciled ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0, adminId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), entryId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(reconciled ? "Entry reconciled." : "Entry unreconciled.", entryId);
    }

    public async Task<ErpSimpleWriteResult> SaveJournalLineDimsAsync(DbConnection c, long lineId, IReadOnlyDictionary<string, string?> f, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        var set = new StringBuilder();
        var args = new List<object?>();
        foreach (var col in JournalLineDimColumns)
        {
            if (!f.TryGetValue(col, out var v)) continue;
            if (set.Length > 0) set.Append(", ");
            set.Append('`').Append(col).Append("` = ?");
            args.Add(v);
        }
        if (set.Length == 0) return ErpSimpleWriteResult.Ok("Nothing to update.", lineId);
        args.Add(lineId);
        var n = await ErpDb.ExecuteAsync(c, null, "UPDATE `epc_erp_gl_lines` SET " + set + " WHERE `id` = ?", t, args.ToArray()).ConfigureAwait(false);
        return n > 0 ? ErpSimpleWriteResult.Ok("Line dimensions saved.", lineId) : ErpSimpleWriteResult.Fail("not_found", "GL line " + lineId + " not found.");
    }

    public async Task<List<Dictionary<string, string?>>> TrialBalanceBySetAsync(DbConnection c, long setId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        var set = await ErpD365DocCycleService.FirstRowAsync(c, "SELECT * FROM `epc_erp_gl_dim_sets` WHERE `id` = ?", t, setId).ConfigureAwait(false);
        if (set is null) return [];
        var comp = (set.TryGetValue("composition", out var cp) ? cp ?? "" : "");
        var members = comp.Split('>', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (members.Length == 0) members = ["main_account"];

        // Pivot: one row per distinct member value; per member, sum debit/credit grouped by main account.
        var rows = new List<Dictionary<string, string?>>();
        foreach (var member in members)
        {
            var sql = member switch
            {
                "legal_entity" => "SELECT a.`account_code` ac, a.`name` an, CAST(l.`legal_entity_id` AS CHAR) dv, SUM(l.`debit`) dr, SUM(l.`credit`) cr FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` GROUP BY a.`id`, l.`legal_entity_id`",
                "business_unit" => "SELECT a.`account_code` ac, a.`name` an, CAST(l.`business_unit_id` AS CHAR) dv, SUM(l.`debit`) dr, SUM(l.`credit`) cr FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` GROUP BY a.`id`, l.`business_unit_id`",
                "cost_centre" => "SELECT a.`account_code` ac, a.`name` an, CAST(l.`cost_centre_id` AS CHAR) dv, SUM(l.`debit`) dr, SUM(l.`credit`) cr FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` GROUP BY a.`id`, l.`cost_centre_id`",
                _ => "SELECT a.`account_code` ac, a.`name` an, '' dv, SUM(l.`debit`) dr, SUM(l.`credit`) cr FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` GROUP BY a.`id`",
            };
            var part = await ErpD365DocCycleService.RowsAsync(c, sql, t).ConfigureAwait(false);
            foreach (var r in part)
            {
                r["member"] = member;
                rows.Add(r);
            }
        }
        return rows;
    }

    public async Task<ErpSimpleWriteResult> SaveCoaFieldsAsync(DbConnection c, long accountId, IReadOnlyDictionary<string, string?> f, int adminId, CancellationToken t = default)
    {
        await EnsureSchemaAsync(c, t).ConfigureAwait(false);
        var set = new StringBuilder();
        var args = new List<object?>();
        foreach (var col in CoaColumns)
        {
            if (!f.TryGetValue(col, out var v)) continue;
            if (set.Length > 0) set.Append(", ");
            set.Append('`').Append(col).Append("` = ?");
            args.Add(v);
        }
        if (set.Length == 0) return ErpSimpleWriteResult.Ok("Nothing to update.", accountId);
        args.Add(accountId);
        var n = await ErpDb.ExecuteAsync(c, null, "UPDATE `epc_erp_coa_accounts` SET " + set + " WHERE `id` = ?", t, args.ToArray()).ConfigureAwait(false);
        return n > 0 ? ErpSimpleWriteResult.Ok("COA fields saved.", accountId) : ErpSimpleWriteResult.Fail("not_found", "Account " + accountId + " not found.");
    }
}
