using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpIntercompanyParty(string Scope, string Code, string Name);

public sealed record ErpIntercompanyRelationship(long Id, string Scope, string FromCode, string ToCode, string Role);

public sealed record ErpIntercompanyAccount(string Purpose, string Scope, string CoaCode, bool Active);

public sealed record ErpIntercompanyRule(string Code, string Name, string DebitPurpose, string CreditPurpose, string Method, bool Active);

public sealed record ErpIntercompanyPolicy(
    bool VatGroup,
    bool CtGroup,
    decimal SalesVatRate,
    decimal PurchaseVatRate,
    string TpMethod,
    string ReportingCurrency,
    string TranslationMethod,
    string TransactionCurrency);

public sealed record ErpIntercompanySetup(
    IReadOnlyList<ErpIntercompanyRelationship> Relationships,
    IReadOnlyList<ErpIntercompanyAccount> Accounts,
    IReadOnlyList<ErpIntercompanyRule> Rules,
    ErpIntercompanyPolicy Policy,
    IReadOnlyList<ErpIntercompanyParty> Parties,
    string Source,
    string Message);

public sealed record ErpIntercompanySaveRequest(
    string Action,
    string Scope,
    string FromCode,
    string ToCode,
    string Role,
    string Purpose,
    string CoaCode,
    string RuleCode,
    string RuleName,
    string DebitPurpose,
    string CreditPurpose,
    string Method,
    bool RuleActive,
    bool VatGroup,
    bool CtGroup,
    decimal SalesVatRate,
    decimal PurchaseVatRate,
    string TpMethod,
    string ReportingCurrency,
    string TranslationMethod,
    string TransactionCurrency,
    int CompanyId);

public sealed record ErpIntercompanySaveResult(bool Succeeded, string Message, int Writes, string Code);

public interface IErpIntercompanySetupService
{
    Task<ErpIntercompanySetup> ReadAsync(CancellationToken cancellationToken = default);

    Task<ErpIntercompanySaveResult> SaveAsync(ErpIntercompanySaveRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpIntercompanySetupService : IErpIntercompanySetupService
{
    public const string AppPath = "/erp/intercompany-app";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpIntercompanySetupService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpIntercompanySetup> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty("migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            return Empty("database-error", ex.Message);
        }

        return new(
            await ReadRelationshipsAsync(connection, cancellationToken).ConfigureAwait(false),
            await ReadAccountsAsync(connection, cancellationToken).ConfigureAwait(false),
            await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false),
            await ReadPolicyAsync(connection, cancellationToken).ConfigureAwait(false),
            await ReadPartiesAsync(connection, cancellationToken).ConfigureAwait(false),
            "live",
            string.Empty);
    }

    public async Task<ErpIntercompanySaveResult> SaveAsync(
        ErpIntercompanySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", 0, "config");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "relationship" => await SaveRelationshipAsync(connection, request, cancellationToken).ConfigureAwait(false),
            "account" => await SaveAccountAsync(connection, request, cancellationToken).ConfigureAwait(false),
            "rule" => await SaveRuleAsync(connection, request, cancellationToken).ConfigureAwait(false),
            "policy" or "tax" or "currency" => await SavePolicyAsync(connection, request, cancellationToken).ConfigureAwait(false),
            _ => new(false, "Choose a relationship, account, elimination rule, or policy.", 0, "action"),
        };
    }

    internal static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpGlChartOfAccountsSeeder.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureCompanyContextAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_ic_relationships` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `scope` varchar(32) NOT NULL DEFAULT 'legal_entity',
              `from_code` varchar(40) NOT NULL DEFAULT '',
              `to_code` varchar(40) NOT NULL DEFAULT '',
              `role` varchar(16) NOT NULL DEFAULT 'both',
              `active` tinyint(1) NOT NULL DEFAULT 1,
              `company_id` int(11) NOT NULL DEFAULT 0,
              `time_created` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `u_scope` (`scope`,`from_code`,`to_code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_ic_accounts` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `purpose` varchar(32) NOT NULL DEFAULT '',
              `coa_code` varchar(16) NOT NULL DEFAULT '',
              `scope` varchar(32) NOT NULL DEFAULT 'legal_entity',
              `active` tinyint(1) NOT NULL DEFAULT 1,
              `time_updated` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `u_purpose` (`purpose`,`scope`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_ic_elim_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `code` varchar(32) NOT NULL DEFAULT '',
              `name` varchar(190) NOT NULL DEFAULT '',
              `debit_purpose` varchar(32) NOT NULL DEFAULT '',
              `credit_purpose` varchar(32) NOT NULL DEFAULT '',
              `method` varchar(16) NOT NULL DEFAULT 'full',
              `rate_pct` decimal(9,4) NOT NULL DEFAULT 0,
              `active` tinyint(1) NOT NULL DEFAULT 1,
              PRIMARY KEY (`id`),
              UNIQUE KEY `u_code` (`code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        // Older tenants: add the unrealized-profit rate column.
        await ErpDb.TryExecuteAsync(connection, """
            ALTER TABLE `epc_erp_ic_elim_rules` ADD COLUMN `rate_pct` decimal(9,4) NOT NULL DEFAULT 0 AFTER `method`
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_ic_policy` (
              `id` int(11) NOT NULL,
              `vat_group` tinyint(1) NOT NULL DEFAULT 0,
              `ct_group` tinyint(1) NOT NULL DEFAULT 0,
              `sales_vat_rate` decimal(6,2) NOT NULL DEFAULT 5.00,
              `purchase_vat_rate` decimal(6,2) NOT NULL DEFAULT 5.00,
              `tp_method` varchar(32) NOT NULL DEFAULT 'cup',
              `reporting_currency` char(3) NOT NULL DEFAULT 'AED',
              `translation_method` varchar(32) NOT NULL DEFAULT 'closing_average',
              `transaction_currency` char(3) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var account in new (string Code, string Name, string Type, string Side)[]
        {
            ("1150", "VAT input (recoverable)", "asset", "debit"),
            ("1180", "Intercompany receivable", "asset", "debit"),
            ("2100", "VAT output (payable)", "liability", "credit"),
            ("2180", "Intercompany payable", "liability", "credit"),
            ("4180", "Intercompany sales", "revenue", "credit"),
            ("5180", "Intercompany purchases", "expense", "debit"),
        })
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`description`,`system_flag`,`time_created`)"
                    + " VALUES (?,?,?,?,?,1,?)"),
                cancellationToken,
                account.Code,
                account.Name,
                account.Type,
                account.Side,
                account.Name,
                now).ConfigureAwait(false);
        }

        foreach (var scope in new[] { "legal_entity", "business_unit", "cost_centre" })
        {
            foreach (var map in new (string Purpose, string Code)[]
            {
                ("sales", "4180"),
                ("purchase", "5180"),
                ("receivable", "1180"),
                ("payable", "2180"),
            })
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "INSERT IGNORE INTO `epc_erp_ic_accounts` (`purpose`,`coa_code`,`scope`,`active`,`time_updated`) VALUES (?,?,?,1,?)"),
                    cancellationToken,
                    map.Purpose,
                    map.Code,
                    scope,
                    now).ConfigureAwait(false);
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT IGNORE INTO `epc_erp_ic_elim_rules` (`code`,`name`,`debit_purpose`,`credit_purpose`,`method`,`active`)"
                + " VALUES ('ELIM-PL','Eliminate intercompany profit and loss','purchase','sales','full',1),"
                + " ('ELIM-BS','Eliminate intercompany receivable and payable','receivable','payable','full',1)"),
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "INSERT IGNORE INTO `epc_erp_ic_policy` (`id`,`vat_group`,`ct_group`,`sales_vat_rate`,`purchase_vat_rate`,`tp_method`,`reporting_currency`,`translation_method`,`transaction_currency`)"
            + " VALUES (1,0,0,5.00,5.00,'cup','AED','closing_average','')",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ErpIntercompanySaveResult> SaveRelationshipAsync(
        DbConnection connection,
        ErpIntercompanySaveRequest request,
        CancellationToken cancellationToken)
    {
        var scope = NormalizeScope(request.Scope);
        var from = CleanCode(request.FromCode);
        var to = CleanCode(request.ToCode);
        var role = NormalizeRole(request.Role);
        if (scope.Length == 0)
        {
            return new(false, "Choose legal entity, business unit, or cost centre.", 0, "scope");
        }

        if (from.Length == 0 || to.Length == 0)
        {
            return new(false, "Enter both parties.", 0, "party");
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, "A party cannot be paired with itself.", 0, "party");
        }

        if (!await PartyExistsAsync(connection, scope, from, cancellationToken).ConfigureAwait(false)
            || !await PartyExistsAsync(connection, scope, to, cancellationToken).ConfigureAwait(false))
        {
            return new(false, "Both parties must already exist in that scope.", 0, "party");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ic_relationships` (`scope`,`from_code`,`to_code`,`role`,`active`,`company_id`,`time_created`)"
                + " VALUES (?,?,?,?,1,?,?)"
                + " ON DUPLICATE KEY UPDATE `role` = VALUES(`role`), `active` = 1, `company_id` = VALUES(`company_id`)"),
            cancellationToken,
            scope,
            from,
            to,
            role,
            request.CompanyId,
            now).ConfigureAwait(false);
        return new(true, "Intercompany relationship saved.", 1, "ok");
    }

    private static async Task<ErpIntercompanySaveResult> SaveAccountAsync(
        DbConnection connection,
        ErpIntercompanySaveRequest request,
        CancellationToken cancellationToken)
    {
        var purpose = NormalizePurpose(request.Purpose);
        var scope = NormalizeScope(request.Scope);
        var code = CleanCode(request.CoaCode);
        if (purpose.Length == 0 || scope.Length == 0 || code.Length == 0)
        {
            return new(false, "Choose the transaction, the scope, and a chart account.", 0, "account");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1"),
            cancellationToken,
            code).ConfigureAwait(false);
        if (found <= 0)
        {
            return new(false, "That chart account is not on the books.", 0, "account");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ic_accounts` (`purpose`,`coa_code`,`scope`,`active`,`time_updated`) VALUES (?,?,?,1,?)"
                + " ON DUPLICATE KEY UPDATE `coa_code` = VALUES(`coa_code`), `active` = 1, `time_updated` = VALUES(`time_updated`)"),
            cancellationToken,
            purpose,
            code,
            scope,
            now).ConfigureAwait(false);
        return new(true, "Intercompany account saved.", 1, "ok");
    }

    private static async Task<ErpIntercompanySaveResult> SaveRuleAsync(
        DbConnection connection,
        ErpIntercompanySaveRequest request,
        CancellationToken cancellationToken)
    {
        var code = CleanCode(request.RuleCode);
        var name = (request.RuleName ?? string.Empty).Trim();
        var debit = NormalizePurpose(request.DebitPurpose);
        var credit = NormalizePurpose(request.CreditPurpose);
        var method = (request.Method ?? string.Empty).Trim().ToLowerInvariant() == "none" ? "none" : "full";
        if (code.Length == 0 || name.Length == 0 || debit.Length == 0 || credit.Length == 0)
        {
            return new(false, "Enter the rule code, name, and both accounts to eliminate.", 0, "rule");
        }

        if (debit == credit)
        {
            return new(false, "Elimination needs two different transaction types.", 0, "rule");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ic_elim_rules` (`code`,`name`,`debit_purpose`,`credit_purpose`,`method`,`active`)"
                + " VALUES (?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `debit_purpose` = VALUES(`debit_purpose`),"
                + " `credit_purpose` = VALUES(`credit_purpose`), `method` = VALUES(`method`), `active` = VALUES(`active`)"),
            cancellationToken,
            code,
            name.Length > 190 ? name[..190] : name,
            debit,
            credit,
            method,
            request.RuleActive ? 1 : 0).ConfigureAwait(false);
        return new(true, "Elimination rule saved.", 1, "ok");
    }

    private static async Task<ErpIntercompanySaveResult> SavePolicyAsync(
        DbConnection connection,
        ErpIntercompanySaveRequest request,
        CancellationToken cancellationToken)
    {
        var current = await ReadPolicyAsync(connection, cancellationToken).ConfigureAwait(false);
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        var sales = action == "currency" ? current.SalesVatRate : ClampRate(request.SalesVatRate);
        var purchase = action == "currency" ? current.PurchaseVatRate : ClampRate(request.PurchaseVatRate);
        var method = action == "currency" ? NormalizeTp(current.TpMethod) : NormalizeTp(request.TpMethod);
        var reporting = action == "tax" ? current.ReportingCurrency : Currency(request.ReportingCurrency, current.ReportingCurrency);
        var transaction = action == "tax" ? current.TransactionCurrency : Currency(request.TransactionCurrency, "");
        var translation = action == "tax"
            ? current.TranslationMethod
            : (request.TranslationMethod ?? string.Empty).Trim().ToLowerInvariant() == "current"
                ? "current"
                : "closing_average";
        var vatGroup = action == "currency" ? current.VatGroup : request.VatGroup;
        var ctGroup = action == "currency" ? current.CtGroup : request.CtGroup;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_ic_policy` (`id`,`vat_group`,`ct_group`,`sales_vat_rate`,`purchase_vat_rate`,`tp_method`,`reporting_currency`,`translation_method`,`transaction_currency`)"
                + " VALUES (1,?,?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `vat_group` = VALUES(`vat_group`), `ct_group` = VALUES(`ct_group`),"
                + " `sales_vat_rate` = VALUES(`sales_vat_rate`), `purchase_vat_rate` = VALUES(`purchase_vat_rate`), `tp_method` = VALUES(`tp_method`),"
                + " `reporting_currency` = VALUES(`reporting_currency`), `translation_method` = VALUES(`translation_method`), `transaction_currency` = VALUES(`transaction_currency`)"),
            cancellationToken,
            vatGroup ? 1 : 0,
            ctGroup ? 1 : 0,
            sales,
            purchase,
            method,
            reporting,
            translation,
            transaction).ConfigureAwait(false);
        return new(true, "Intercompany policy saved.", 1, "ok");
    }

    private static async Task<bool> PartyExistsAsync(
        DbConnection connection,
        string scope,
        string code,
        CancellationToken cancellationToken)
    {
        var table = scope switch
        {
            "business_unit" => "epc_erp_pm_business_units",
            "cost_centre" => "epc_cc_centers",
            _ => "epc_erp_pm_legal_entities",
        };
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `" + table + "` WHERE `active` = 1 AND `code` = ?"),
            cancellationToken,
            code).ConfigureAwait(false);
        return count > 0;
    }

    private static async Task<IReadOnlyList<ErpIntercompanyRelationship>> ReadRelationshipsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpIntercompanyRelationship>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `id`, IFNULL(`scope`, '') AS scope, IFNULL(`from_code`, '') AS from_code,
                       IFNULL(`to_code`, '') AS to_code, IFNULL(`role`, '') AS role
                FROM `epc_erp_ic_relationships`
                WHERE `active` = 1
                ORDER BY `scope`, `from_code`, `to_code`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpIntercompanyAccount>> ReadAccountsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpIntercompanyAccount>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`purpose`, ''), IFNULL(`scope`, ''), IFNULL(`coa_code`, ''), IFNULL(`active`, 0)
                FROM `epc_erp_ic_accounts`
                ORDER BY `purpose`, `scope`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) == 1));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpIntercompanyRule>> ReadRulesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpIntercompanyRule>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`code`, ''), IFNULL(`name`, ''), IFNULL(`debit_purpose`, ''),
                       IFNULL(`credit_purpose`, ''), IFNULL(`method`, ''), IFNULL(`active`, 0)
                FROM `epc_erp_ic_elim_rules`
                ORDER BY `code`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) == 1));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<ErpIntercompanyPolicy> ReadPolicyAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`vat_group`, 0), IFNULL(`ct_group`, 0), IFNULL(`sales_vat_rate`, 5),
                       IFNULL(`purchase_vat_rate`, 5), IFNULL(`tp_method`, 'cup'), IFNULL(`reporting_currency`, 'AED'),
                       IFNULL(`translation_method`, 'closing_average'), IFNULL(`transaction_currency`, '')
                FROM `epc_erp_ic_policy`
                WHERE `id` = 1
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new(
                    Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) == 1,
                    Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) == 1,
                    Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7));
            }
        }
        catch (DbException)
        {
            // The default policy is shown until the table exists.
        }

        return DefaultPolicy();
    }

    private static async Task<IReadOnlyList<ErpIntercompanyParty>> ReadPartiesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpIntercompanyParty>();
        await AddPartiesAsync(rows, connection, "legal_entity", "epc_erp_pm_legal_entities", cancellationToken).ConfigureAwait(false);
        await AddPartiesAsync(rows, connection, "business_unit", "epc_erp_pm_business_units", cancellationToken).ConfigureAwait(false);
        await AddPartiesAsync(rows, connection, "cost_centre", "epc_cc_centers", cancellationToken).ConfigureAwait(false);
        return rows;
    }

    private static async Task AddPartiesAsync(
        List<ErpIntercompanyParty> rows,
        DbConnection connection,
        string scope,
        string table,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT IFNULL(`code`, ''), IFNULL(`name`, '') FROM `" + table + "` WHERE `active` = 1 ORDER BY `code`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = reader.GetString(0).Trim();
                if (code.Length > 0)
                {
                    rows.Add(new(scope, code, reader.GetString(1)));
                }
            }
        }
        catch (DbException)
        {
            // A missing party table leaves that scope empty.
        }
    }

    private static ErpIntercompanySetup Empty(string source, string message)
        => new([], [], [], DefaultPolicy(), [], source, message);

    private static ErpIntercompanyPolicy DefaultPolicy()
        => new(false, false, 5m, 5m, "cup", "AED", "closing_average", "");

    private static string NormalizeScope(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "legal_entity" or "legal" or "entity" => "legal_entity",
            "business_unit" or "bu" or "unit" => "business_unit",
            "cost_centre" or "cost_center" or "cc" => "cost_centre",
            _ => "",
        };

    private static string NormalizePurpose(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "sales" or "sale" => "sales",
            "purchase" or "purchases" => "purchase",
            "receivable" or "receivables" => "receivable",
            "payable" or "payables" => "payable",
            _ => "",
        };

    private static string NormalizeRole(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "customer" => "customer",
            "vendor" or "supplier" => "vendor",
            _ => "both",
        };

    private static string NormalizeTp(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "resale" => "resale",
            "cost_plus" => "cost_plus",
            "tnmm" => "tnmm",
            "profit_split" => "profit_split",
            _ => "cup",
        };

    private static string CleanCode(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length > 40 ? text[..40] : text;
    }

    private static decimal ClampRate(decimal rate)
    {
        if (rate < 0)
        {
            return 0;
        }

        return rate > 100 ? 100 : rate;
    }

    private static string Currency(string? raw, string fallback)
    {
        var text = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (text.Length == 0)
        {
            return fallback;
        }

        return text.Length == 3 && text.All(char.IsLetter) ? text : fallback;
    }
}
