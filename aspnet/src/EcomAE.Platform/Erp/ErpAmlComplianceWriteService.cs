using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_aml_check_transaction</c> twin (ajax <c>aml_check</c>):
/// ensures the AML schema, seeds default rules, scores the transaction, inserts
/// <c>epc_aml_transactions</c> and returns the PHP flag payload.
/// </summary>
public interface IErpAmlCheckWriteService
{
    Task<ErpAmlCheckResult> CheckAsync(ErpAmlCheckWriteRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>epc_aml_seed_rules</c> twin (ajax <c>aml_seed_rules</c>).</summary>
public interface IErpAmlSeedRulesWriteService
{
    Task<ErpAmlSeedResult> SeedAsync(long companyHint, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>epc_aml_settings_save</c> twin (ajax <c>aml_settings_save</c>).</summary>
public interface IErpAmlSettingsSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(ErpAmlSettingsWriteInput input, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>epc_aml_generate_report</c> twin (ajax <c>aml_report_generate</c>).</summary>
public interface IErpAmlReportGenerateWriteService
{
    Task<ErpAmlReportResult> GenerateAsync(string? reportType, string? periodFrom, string? periodTo, int userId, CancellationToken cancellationToken = default);
}

public sealed record ErpAmlCheckWriteRequest(
    long CompanyHint = 0,
    long CustomerId = 0,
    string? CustomerName = null,
    string? TransactionType = null,
    decimal Amount = 0m,
    string? Currency = null,
    string? Reference = null);

public sealed record ErpAmlCheckResult(
    bool Ok,
    bool Flagged,
    int RiskScore,
    IReadOnlyList<string> Flags,
    long TransactionId,
    string Message);

public sealed record ErpAmlSeedResult(bool Ok, int Created, string Message);

public sealed record ErpAmlSettingsWriteInput(
    string? CashThreshold = null,
    bool StructuringEnabled = false,
    bool PepScreening = false,
    string? Authority = null,
    int KycLowMonths = 0,
    int KycMediumMonths = 0,
    int KycHighMonths = 0,
    string? MlroName = null,
    string? GoamlReg = null);

public sealed record ErpAmlReportResult(
    bool Ok,
    long Id,
    string? Title,
    string? FileReference,
    string Message);

public sealed record ErpAmlRuleRow(
    string Name,
    string Type,
    decimal ThresholdAmount,
    string ThresholdCurrency,
    int FrequencyCount,
    int FrequencyPeriodDays,
    string Action);

public sealed record ErpAmlKycRow(
    string RiskLevel,
    bool PepStatus,
    bool SanctionsMatch,
    string VerificationStatus,
    string CustomerName);

public sealed record ErpAmlAlertRow(
    long Id,
    long CustomerId,
    string CustomerName,
    decimal Amount,
    string Currency,
    int RiskScore,
    string FlagReason,
    string ReviewStatus,
    long TimeCreated);

public static class ErpAmlShared
{
    public static readonly (string Name, string Type, decimal ThresholdAmount, string Currency, string Action, int FreqCount, int FreqDays)[] DefaultRules =
    {
        ("Cash transaction ≥ 55,000 AED (DPMS / CTR)", "threshold", 55000m, "AED", "report", 0, 1),
        ("Wire transfer ≥ 100,000 AED", "threshold", 100000m, "AED", "flag", 0, 1),
        ("More than 5 cash transactions in 7 days", "frequency", 0m, "AED", "flag", 5, 7),
        ("Structuring — 3+ payments in 24h near cash threshold", "frequency", 0m, "AED", "flag", 3, 1),
        ("High-risk country origin", "country", 0m, "AED", "flag", 0, 1),
    };

    private static readonly string[] SchemaStatements =
    {
        "CREATE TABLE IF NOT EXISTS `epc_aml_kyc` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0, `customer_id` int(11) NOT NULL DEFAULT 0, `customer_name` varchar(200) NOT NULL DEFAULT '', `id_type` varchar(50) NOT NULL DEFAULT '', `id_number` varchar(100) NOT NULL DEFAULT '', `id_expiry` date DEFAULT NULL, `id_document_path` varchar(500) NOT NULL DEFAULT '', `nationality` varchar(50) NOT NULL DEFAULT '', `dob` date DEFAULT NULL, `risk_level` varchar(20) NOT NULL DEFAULT 'low', `pep_status` tinyint(1) NOT NULL DEFAULT 0, `sanctions_checked` tinyint(1) NOT NULL DEFAULT 0, `sanctions_match` tinyint(1) NOT NULL DEFAULT 0, `verification_status` varchar(20) NOT NULL DEFAULT 'pending', `verified_by` int(11) NOT NULL DEFAULT 0, `verified_at` datetime DEFAULT NULL, `next_review` date DEFAULT NULL, `notes` text, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_company` (`company_id`), KEY `x_customer` (`customer_id`), KEY `x_risk` (`risk_level`), KEY `x_status` (`verification_status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
        "CREATE TABLE IF NOT EXISTS `epc_aml_transactions` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0, `customer_id` int(11) NOT NULL DEFAULT 0, `customer_name` varchar(200) NOT NULL DEFAULT '', `transaction_type` varchar(30) NOT NULL DEFAULT '', `amount` decimal(14,2) NOT NULL DEFAULT 0.00, `currency` varchar(3) NOT NULL DEFAULT 'AED', `reference` varchar(100) NOT NULL DEFAULT '', `risk_score` int(11) NOT NULL DEFAULT 0, `flagged` tinyint(1) NOT NULL DEFAULT 0, `flag_reason` varchar(300) NOT NULL DEFAULT '', `review_status` varchar(30) NOT NULL DEFAULT 'open', `sar_filed` tinyint(1) NOT NULL DEFAULT 0, `sar_reference` varchar(64) NOT NULL DEFAULT '', `reviewed_by` int(11) NOT NULL DEFAULT 0, `reviewed_at` datetime DEFAULT NULL, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_company` (`company_id`), KEY `x_customer` (`customer_id`), KEY `x_flagged` (`flagged`), KEY `x_amount` (`amount`), KEY `x_review` (`review_status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
        "CREATE TABLE IF NOT EXISTS `epc_aml_rules` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0, `rule_name` varchar(200) NOT NULL DEFAULT '', `rule_type` varchar(50) NOT NULL DEFAULT '', `threshold_amount` decimal(14,2) NOT NULL DEFAULT 0.00, `threshold_currency` varchar(3) NOT NULL DEFAULT 'AED', `frequency_count` int(11) NOT NULL DEFAULT 0, `frequency_period_days` int(11) NOT NULL DEFAULT 1, `countries_list` text, `action` varchar(50) NOT NULL DEFAULT 'flag', `is_active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_company` (`company_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
        "CREATE TABLE IF NOT EXISTS `epc_aml_reports` (`id` int(11) NOT NULL AUTO_INCREMENT, `company_id` int(11) NOT NULL DEFAULT 0, `report_type` varchar(50) NOT NULL DEFAULT '', `title` varchar(200) NOT NULL DEFAULT '', `period_from` date DEFAULT NULL, `period_to` date DEFAULT NULL, `total_transactions` int(11) NOT NULL DEFAULT 0, `flagged_transactions` int(11) NOT NULL DEFAULT 0, `sar_count` int(11) NOT NULL DEFAULT 0, `summary_json` mediumtext, `body_html` mediumtext, `filed_to` varchar(200) NOT NULL DEFAULT '', `file_reference` varchar(100) NOT NULL DEFAULT '', `generated_by` int(11) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_company` (`company_id`), KEY `x_type` (`report_type`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
    };

    private static readonly (string Table, string Column, string Definition)[] ColumnUpgrades =
    {
        ("epc_aml_kyc", "next_review", "date DEFAULT NULL"),
        ("epc_aml_transactions", "customer_name", "varchar(200) NOT NULL DEFAULT ''"),
        ("epc_aml_transactions", "review_status", "varchar(30) NOT NULL DEFAULT 'open'"),
        ("epc_aml_reports", "title", "varchar(200) NOT NULL DEFAULT ''"),
        ("epc_aml_reports", "summary_json", "mediumtext"),
        ("epc_aml_reports", "body_html", "mediumtext"),
    };

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in SchemaStatements)
        {
            await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }

        foreach (var (table, column, definition) in ColumnUpgrades)
        {
            var present = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
                cancellationToken,
                table,
                column).ConfigureAwait(false) > 0;
            if (!present)
            {
                await ErpDb.TryExecuteAsync(
                    connection,
                    "ALTER TABLE `" + table + "` ADD COLUMN `" + column + "` " + definition,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>PHP <c>epc_erp_adv_settings_ensure</c>: settings live in <c>epc_price_settings</c> with keys prefixed <c>aml_</c>.</summary>
    public static async Task EnsureSettingsSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_price_settings` (`setting_key` varchar(128) NOT NULL, `setting_value` text, PRIMARY KEY (`setting_key`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<string> SettingGetAsync(DbConnection connection, string key, string fallback, CancellationToken cancellationToken)
    {
        await EnsureSettingsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var value = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
            cancellationToken,
            "aml_" + key).ConfigureAwait(false);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    public static async Task SettingSetAsync(DbConnection connection, string key, string value, CancellationToken cancellationToken)
    {
        await EnsureSettingsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_price_settings` (`setting_key`,`setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            "aml_" + key,
            value).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_aml_company_id</c>: falls back to <c>epc_erp_active_company_id</c> (first active legal entity, or the hint when active).</summary>
    public static Task<long> ResolveCompanyIdAsync(DbConnection connection, long companyHint, CancellationToken cancellationToken)
        => ErpFinAdvancedCompany.ResolveAsync(connection, companyHint, cancellationToken);

    /// <summary>PHP <c>epc_aml_seed_rules</c>.</summary>
    public static async Task<ErpAmlSeedResult> SeedRulesAsync(DbConnection connection, long companyHint, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = companyHint > 0 ? companyHint : await ResolveCompanyIdAsync(connection, companyHint, cancellationToken).ConfigureAwait(false);
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_aml_rules` WHERE `company_id` = ?"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        if (existing > 0)
        {
            return new ErpAmlSeedResult(true, 0, "Rules already configured");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var created = 0;
        foreach (var rule in DefaultRules)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_aml_rules` (`company_id`,`rule_name`,`rule_type`,`threshold_amount`,`threshold_currency`,`frequency_count`,`frequency_period_days`,`action`,`is_active`,`time_created`) VALUES (?,?,?,?,?,?,?,?,1,?)"),
                cancellationToken,
                companyId,
                rule.Name,
                rule.Type,
                rule.ThresholdAmount,
                rule.Currency,
                rule.FreqCount,
                rule.FreqDays,
                rule.Action,
                now).ConfigureAwait(false);
            created++;
        }

        if ((await SettingGetAsync(connection, "cash_threshold", "", cancellationToken).ConfigureAwait(false)).Length == 0)
        {
            await SettingSetAsync(connection, "cash_threshold", "55000", cancellationToken).ConfigureAwait(false);
        }

        if ((await SettingGetAsync(connection, "authority", "", cancellationToken).ConfigureAwait(false)).Length == 0)
        {
            await SettingSetAsync(connection, "authority", "UAE FIU (goAML)", cancellationToken).ConfigureAwait(false);
        }

        return new ErpAmlSeedResult(true, created, "Seeded " + created.ToString(CultureInfo.InvariantCulture) + " default AML rules");
    }

    public static async Task<IReadOnlyList<ErpAmlRuleRow>> ListRulesAsync(DbConnection connection, long companyId, CancellationToken cancellationToken)
    {
        var rules = new List<ErpAmlRuleRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `rule_name`,`rule_type`,`threshold_amount`,`threshold_currency`,`frequency_count`,`frequency_period_days`,`action` FROM `epc_aml_rules` WHERE `company_id` = ? AND `is_active` = 1");
        ErpDb.AddParameters(command, companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rules.Add(new ErpAmlRuleRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDecimal(2),
                reader.GetString(3),
                Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                reader.GetString(6)));
        }

        return rules;
    }

    public static async Task<ErpAmlKycRow?> LatestKycAsync(DbConnection connection, long companyId, long customerId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `risk_level`, `pep_status`, `sanctions_match`, `verification_status`, `customer_name` FROM `epc_aml_kyc` WHERE `company_id` = ? AND `customer_id` = ? ORDER BY `time_created` DESC LIMIT 1");
        ErpDb.AddParameters(command, companyId, customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ErpAmlKycRow(
            reader.GetString(0),
            Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0,
            Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) != 0,
            reader.GetString(3),
            reader.IsDBNull(4) ? "" : reader.GetString(4));
    }

    public static async Task<long> CountRecentTransactionsAsync(DbConnection connection, long companyId, long customerId, long sinceEpoch, CancellationToken cancellationToken)
    {
        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `customer_id` = ? AND `time_created` > ?"),
            cancellationToken,
            companyId,
            customerId,
            sinceEpoch).ConfigureAwait(false);
    }

    public static async Task<Dictionary<string, object?>> DashboardAsync(DbConnection connection, long companyId, long fromTs, long toTs, CancellationToken cancellationToken)
    {
        Task<long> Count(string sql, params object[] args) => ErpDb.LongAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args);

        var kycTotal = await Count("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ?", companyId);
        var kycVerified = await Count("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `verification_status` = 'verified'", companyId);
        var highRisk = await Count("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `risk_level` IN ('high','very_high')", companyId);
        var pepCount = await Count("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `pep_status` = 1", companyId);
        var kycPending = await Count("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `verification_status` IN ('pending','expired')", companyId);
        var txTotal = await Count("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `time_created` BETWEEN ? AND ?", companyId, fromTs, toTs);
        var flagged = await Count("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `flagged` = 1 AND `time_created` BETWEEN ? AND ?", companyId, fromTs, toTs);
        var openAlerts = await Count("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `flagged` = 1 AND `review_status` = 'open'", companyId);
        var sarFiled = await Count("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `sar_filed` = 1 AND `time_created` BETWEEN ? AND ?", companyId, fromTs, toTs);
        var ctrFiled = await Count("SELECT COUNT(*) FROM `epc_aml_reports` WHERE `company_id` = ? AND `report_type` = 'ctr' AND `time_created` BETWEEN ? AND ?", companyId, fromTs, toTs);
        var rulesActive = await Count("SELECT COUNT(*) FROM `epc_aml_rules` WHERE `company_id` = ? AND `is_active` = 1", companyId);

        return new Dictionary<string, object?>
        {
            ["kyc_total"] = kycTotal,
            ["kyc_verified"] = kycVerified,
            ["kyc_pending"] = kycPending,
            ["kyc_pct"] = kycTotal > 0 ? (long)Math.Round((double)kycVerified / kycTotal * 100) : 0L,
            ["high_risk"] = highRisk,
            ["pep_count"] = pepCount,
            ["tx_total"] = txTotal,
            ["flagged"] = flagged,
            ["open_alerts"] = openAlerts,
            ["sar_filed"] = sarFiled,
            ["ctr_filed"] = ctrFiled,
            ["rules_active"] = rulesActive,
            ["cash_threshold"] = decimal.TryParse(await SettingGetAsync(connection, "cash_threshold", "55000", cancellationToken).ConfigureAwait(false), NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 55000m,
            ["authority"] = await SettingGetAsync(connection, "authority", "UAE FIU (goAML)", cancellationToken).ConfigureAwait(false),
        };
    }

    public static async Task<List<ErpAmlAlertRow>> ListAlertsAsync(DbConnection connection, long companyId, int limit, CancellationToken cancellationToken)
    {
        var rows = new List<ErpAmlAlertRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`,`customer_id`,`customer_name`,`amount`,`currency`,`risk_score`,`flag_reason`,`review_status`,`time_created` FROM `epc_aml_transactions` WHERE `company_id` = "
            + companyId.ToString(CultureInfo.InvariantCulture)
            + " AND `flagged` = 1 ORDER BY `time_created` DESC LIMIT " + Math.Clamp(limit, 1, 200).ToString(CultureInfo.InvariantCulture);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpAmlAlertRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                reader.IsDBNull(2) ? "" : reader.GetString(2),
                reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                reader.IsDBNull(4) ? "" : reader.GetString(4),
                Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture),
                reader.IsDBNull(6) ? "" : reader.GetString(6),
                reader.IsDBNull(7) ? "" : reader.GetString(7),
                Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    /// <summary>PHP <c>epc_aml_list_kyc</c> row plus <c>id_type</c> for the register table.</summary>
    public static async Task<List<(string CustomerName, string IdType, string VerificationStatus, string RiskLevel, bool PepStatus)>> ListKycForReportAsync(DbConnection connection, long companyId, int limit, CancellationToken cancellationToken)
    {
        var kyc = new List<(string CustomerName, string IdType, string VerificationStatus, string RiskLevel, bool PepStatus)>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `customer_name`,`id_type`,`verification_status`,`risk_level`,`pep_status` FROM `epc_aml_kyc` WHERE `company_id` = "
            + companyId.ToString(CultureInfo.InvariantCulture)
            + " ORDER BY FIELD(`risk_level`,'very_high','high','medium','low'), `time_updated` DESC LIMIT " + Math.Clamp(limit, 1, 500).ToString(CultureInfo.InvariantCulture);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            kyc.Add((
                reader.IsDBNull(0) ? "" : reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1),
                reader.IsDBNull(2) ? "pending" : reader.GetString(2),
                reader.IsDBNull(3) ? "low" : reader.GetString(3),
                !reader.IsDBNull(4) && Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0));
        }

        return kyc;
    }
}

public sealed class ErpAmlCheckWriteService : IErpAmlCheckWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAmlCheckWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpAmlCheckResult> CheckAsync(ErpAmlCheckWriteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpAmlShared.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpAmlShared.ResolveCompanyIdAsync(connection, request.CompanyHint, cancellationToken).ConfigureAwait(false);
        await ErpAmlShared.SeedRulesAsync(connection, companyId, cancellationToken).ConfigureAwait(false);

        var flags = new List<string>();
        var riskScore = 0;
        var currency = (request.Currency ?? "AED").Trim();
        currency = (currency.Length == 0 ? "AED" : currency).ToUpperInvariant();
        if (currency.Length > 3)
        {
            currency = currency[..3];
        }

        var rules = await ErpAmlShared.ListRulesAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        if (rules.Count == 0)
        {
            var thresholdSetting = await ErpAmlShared.SettingGetAsync(connection, "cash_threshold", "55000", cancellationToken).ConfigureAwait(false);
            var threshold = decimal.TryParse(thresholdSetting, NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 55000m;
            if (request.Amount >= threshold && currency == "AED")
            {
                flags.Add("Exceeds cash reporting threshold (" + threshold.ToString("N0", CultureInfo.InvariantCulture) + " AED)");
                riskScore += 40;
            }
        }

        foreach (var rule in rules)
        {
            if (rule.Type == "threshold" && request.Amount >= rule.ThresholdAmount && currency == rule.ThresholdCurrency)
            {
                flags.Add("Exceeds threshold: " + rule.Name);
                riskScore += rule.Action == "report" ? 40 : 30;
            }

            if (rule.Type == "frequency")
            {
                var since = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - Math.Max(1, rule.FrequencyPeriodDays) * 86400L;
                var count = await ErpAmlShared.CountRecentTransactionsAsync(connection, companyId, request.CustomerId, since, cancellationToken).ConfigureAwait(false);
                if (count >= rule.FrequencyCount)
                {
                    flags.Add("Frequency exceeded: " + rule.Name);
                    riskScore += 25;
                }
            }
        }

        var kyc = request.CustomerId > 0
            ? await ErpAmlShared.LatestKycAsync(connection, companyId, request.CustomerId, cancellationToken).ConfigureAwait(false)
            : null;
        if (kyc is not null)
        {
            if (kyc.RiskLevel == "high")
            {
                riskScore += 20;
            }

            if (kyc.RiskLevel == "very_high")
            {
                riskScore += 40;
            }

            if (kyc.PepStatus)
            {
                flags.Add("Customer is marked PEP");
                riskScore += 15;
            }

            if (kyc.SanctionsMatch)
            {
                flags.Add("Sanctions match on KYC file");
                riskScore += 50;
            }

            if (kyc.VerificationStatus is "pending" or "expired")
            {
                flags.Add("KYC not verified / expired");
                riskScore += 15;
            }
        }
        else if (request.CustomerId > 0 && request.Amount >= 10000m)
        {
            flags.Add("No KYC record for customer");
            riskScore += 20;
        }

        var flagged = riskScore >= 50 || flags.Count > 0;
        var customerName = (request.CustomerName ?? string.Empty).Trim();
        if (customerName.Length == 0)
        {
            customerName = (kyc?.CustomerName ?? string.Empty).Trim();
        }

        var txType = (request.TransactionType ?? string.Empty).Trim();
        if (txType.Length == 0)
        {
            txType = "cash_sale";
        }

        var reference = (request.Reference ?? string.Empty).Trim();
        var score = Math.Min(100, riskScore);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_aml_transactions` (`company_id`,`customer_id`,`customer_name`,`transaction_type`,`amount`,`currency`,`reference`,`risk_score`,`flagged`,`flag_reason`,`review_status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            request.CustomerId,
            customerName,
            txType,
            request.Amount,
            currency,
            reference,
            score,
            flagged ? 1 : 0,
            string.Join("; ", flags),
            flagged ? "open" : "closed",
            now).ConfigureAwait(false);
        var txId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        return new ErpAmlCheckResult(true, flagged, score, flags, txId, flagged ? "FLAGGED — review required" : "CLEAR");
    }
}

public sealed class ErpAmlSeedRulesWriteService : IErpAmlSeedRulesWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAmlSeedRulesWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpAmlSeedResult> SeedAsync(long companyHint, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var companyId = await ErpAmlShared.ResolveCompanyIdAsync(connection, companyHint, cancellationToken).ConfigureAwait(false);
        return await ErpAmlShared.SeedRulesAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ErpAmlSettingsSaveWriteService : IErpAmlSettingsSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAmlSettingsSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(ErpAmlSettingsWriteInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var thresholdDigits = Regex.Replace(input.CashThreshold ?? "55000", "[^\\d.]", "");
        var cashThreshold = thresholdDigits.Length == 0 ? "55000" : thresholdDigits;
        var map = new (string Key, string Value)[]
        {
            ("cash_threshold", cashThreshold),
            ("structuring_enabled", input.StructuringEnabled ? "1" : "0"),
            ("pep_screening", input.PepScreening ? "1" : "0"),
            ("authority", (input.Authority ?? "UAE FIU (goAML)").Trim()),
            ("kyc_low_months", Math.Max(1, input.KycLowMonths <= 0 ? 12 : input.KycLowMonths).ToString(CultureInfo.InvariantCulture)),
            ("kyc_medium_months", Math.Max(1, input.KycMediumMonths <= 0 ? 6 : input.KycMediumMonths).ToString(CultureInfo.InvariantCulture)),
            ("kyc_high_months", Math.Max(1, input.KycHighMonths <= 0 ? 3 : input.KycHighMonths).ToString(CultureInfo.InvariantCulture)),
            ("mlro_name", (input.MlroName ?? string.Empty).Trim()),
            ("goaml_reg", (input.GoamlReg ?? string.Empty).Trim()),
        };
        foreach (var (key, value) in map)
        {
            await ErpAmlShared.SettingSetAsync(connection, key, value, cancellationToken).ConfigureAwait(false);
        }

        var companyId = await ErpAmlShared.ResolveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);
        await ErpAmlShared.SeedRulesAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_aml_rules` SET `threshold_amount` = ? WHERE `company_id` = ? AND `rule_type` = 'threshold' AND `rule_name` LIKE '%55,000%'"),
            cancellationToken,
            decimal.Parse(cashThreshold, CultureInfo.InvariantCulture),
            companyId).ConfigureAwait(false);

        return new ErpSimpleWriteResult(true, "ok", "AML settings saved", 0, map.Length + 1);
    }
}

public sealed class ErpAmlReportGenerateWriteService : IErpAmlReportGenerateWriteService
{
    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal)
    {
        ["compliance_summary"] = "AML compliance summary",
        ["ctr"] = "Cash transaction report pack (CTR)",
        ["monitoring"] = "Transaction monitoring log",
        ["kyc_register"] = "KYC / CDD register extract",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpAmlReportGenerateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpAmlReportResult> GenerateAsync(string? reportType, string? periodFrom, string? periodTo, int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpAmlShared.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpAmlShared.ResolveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        var type = Regex.Replace((reportType ?? "compliance_summary").ToLowerInvariant(), "[^a-z_]", "");
        if (type.Length == 0 || !Titles.ContainsKey(type))
        {
            type = "compliance_summary";
        }

        var from = (periodFrom ?? "").Trim();
        var to = (periodTo ?? "").Trim();
        if (from.Length == 0)
        {
            from = DateTime.Now.ToString("yyyy-MM-01", CultureInfo.InvariantCulture);
        }

        if (to.Length == 0)
        {
            to = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fromTs = ParseDateEpoch(from, false) ?? nowTs - 30L * 86400L;
        var toTs = ParseDateEpoch(to, true) ?? nowTs;

        var dash = await ErpAmlShared.DashboardAsync(connection, companyId, fromTs, toTs, cancellationToken).ConfigureAwait(false);
        var alerts = await ErpAmlShared.ListAlertsAsync(connection, companyId, 100, cancellationToken).ConfigureAwait(false);
        var kycRows = await ErpAmlShared.ListKycForReportAsync(connection, companyId, 200, cancellationToken).ConfigureAwait(false);

        var cashThreshold = (decimal)(dash["cash_threshold"] ?? 55000m);
        var authority = (string?)(dash["authority"] ?? "UAE FIU (goAML)") ?? "UAE FIU (goAML)";

        var title = Titles[type] + " · " + from + " → " + to;
        string table;
        if (type == "kyc_register")
        {
            var rowsHtml = string.Concat(kycRows.Select(r =>
                "<tr><td>" + WebUtility.HtmlEncode(r.CustomerName) + "</td><td>"
                + WebUtility.HtmlEncode(r.IdType) + "</td><td>"
                + WebUtility.HtmlEncode(r.VerificationStatus) + "</td><td>"
                + WebUtility.HtmlEncode(r.RiskLevel) + "</td><td>"
                + (r.PepStatus ? "PEP" : "—") + "</td></tr>"));
            table = "<table class=\"table table-bordered table-condensed\"><thead><tr><th>Customer</th><th>ID type</th><th>Status</th><th>Risk</th><th>PEP</th></tr></thead><tbody>"
                + (rowsHtml.Length > 0 ? rowsHtml : "<tr><td colspan=\"5\">No KYC records</td></tr>") + "</tbody></table>";
        }
        else
        {
            var rowsHtml = string.Concat(alerts
                .Where(r => type != "ctr" || r.Amount >= cashThreshold)
                .Select(r =>
                    "<tr><td>" + DateTimeOffset.FromUnixTimeSeconds(r.TimeCreated).LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "</td><td>"
                    + WebUtility.HtmlEncode(r.CustomerName.Length > 0 ? r.CustomerName : "#" + r.CustomerId.ToString(CultureInfo.InvariantCulture)) + "</td><td>"
                    + r.Amount.ToString("N2", CultureInfo.InvariantCulture) + " " + WebUtility.HtmlEncode(r.Currency) + "</td><td>"
                    + r.RiskScore.ToString(CultureInfo.InvariantCulture) + "</td><td>"
                    + WebUtility.HtmlEncode(r.FlagReason) + "</td><td>"
                    + WebUtility.HtmlEncode(r.ReviewStatus) + "</td></tr>"));
            table = "<table class=\"table table-bordered table-condensed\"><thead><tr><th>When</th><th>Customer</th><th>Amount</th><th>Score</th><th>Flags</th><th>Status</th></tr></thead><tbody>"
                + (rowsHtml.Length > 0 ? rowsHtml : "<tr><td colspan=\"6\">No matching alerts in period</td></tr>") + "</tbody></table>";
        }

        var body = "<div class=\"epc-aml-report-body\">"
            + "<p><strong>" + WebUtility.HtmlEncode(title) + "</strong><br>"
            + "Authority: " + WebUtility.HtmlEncode(authority) + " · Cash threshold: "
            + cashThreshold.ToString("N0", CultureInfo.InvariantCulture) + " AED</p>"
            + "<div class=\"row\">"
            + "<div class=\"col-sm-3\"><div class=\"well well-sm\">KYC verified<br><b>" + Convert.ToInt64(dash["kyc_pct"], CultureInfo.InvariantCulture) + "%</b></div></div>"
            + "<div class=\"col-sm-3\"><div class=\"well well-sm\">Checks<br><b>" + Convert.ToInt64(dash["tx_total"], CultureInfo.InvariantCulture) + "</b></div></div>"
            + "<div class=\"col-sm-3\"><div class=\"well well-sm\">Flagged<br><b>" + Convert.ToInt64(dash["flagged"], CultureInfo.InvariantCulture) + "</b></div></div>"
            + "<div class=\"col-sm-3\"><div class=\"well well-sm\">STR filed<br><b>" + Convert.ToInt64(dash["sar_filed"], CultureInfo.InvariantCulture) + "</b></div></div>"
            + "</div>"
            + table
            + "<p class=\"text-muted small\">Generated in ERP AML module. File STR/SAR on the official goAML portal when suspicion is confirmed. Do not tip off the subject.</p>"
            + "</div>";

        var summary = JsonSerializer.Serialize(new
        {
            dashboard = dash,
            alert_count = alerts.Count,
            kyc_count = kycRows.Count,
            type,
        });
        var reference = "AML-" + type[..Math.Min(3, type.Length)].ToUpperInvariant() + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_aml_reports` (`company_id`,`report_type`,`title`,`period_from`,`period_to`,`total_transactions`,`flagged_transactions`,`sar_count`,`summary_json`,`body_html`,`filed_to`,`file_reference`,`generated_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            type,
            title,
            DateTimeOffset.FromUnixTimeSeconds(fromTs).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset.FromUnixTimeSeconds(toTs).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Convert.ToInt64(dash["tx_total"], CultureInfo.InvariantCulture),
            Convert.ToInt64(dash["flagged"], CultureInfo.InvariantCulture),
            Convert.ToInt64(dash["sar_filed"], CultureInfo.InvariantCulture),
            summary,
            body,
            authority,
            reference,
            userId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        return new ErpAmlReportResult(true, id, title, reference, "Report generated");
    }

    private static long? ParseDateEpoch(string date, bool endOfDay)
    {
        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return null;
        }

        var day = endOfDay ? parsed.Date.AddDays(1).AddTicks(-1) : parsed.Date;
        return new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day)).ToUnixTimeSeconds();
    }
}
