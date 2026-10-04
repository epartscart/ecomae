using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_compliance_fetch_updates</c> twin (epc_bos_compliance.php):
/// reconciles the tenant's obligations and retention rules against the regulatory
/// catalog for the tenant country/industry; updates only <c>is_seed=1</c> rows and
/// records the synced catalog version.
/// </summary>
public interface IErpBosComplianceFetchService
{
    Task<ErpBosComplianceFetchResult> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpBosComplianceFetchResult(
    bool Ok,
    string Message,
    string Version,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Updated,
    int Writes);

public sealed class ErpBosComplianceFetchService : IErpBosComplianceFetchService
{
    internal const string CatalogVersion = "2026.06.2";

    private static readonly string[] PreciousNeedles = { "jewel", "gold", "precious", "diamond", "bullion" };
    private static readonly string[] DnfbpNeedles = { "jewel", "gold", "precious", "diamond", "bullion", "real_estate", "real-estate", "realestate", "property", "auditor", "accountant", "legal", "corporate_service", "trust" };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpBosComplianceFetchService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    private sealed record Obligation(string Code, string Title, string Regime, string Authority, string Frequency, int LeadDays, string DocRequirements);
    private sealed record Retention(string DocType, string Label, int RetentionYears, string Basis, string LegalRef);

    public async Task<ErpBosComplianceFetchResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", string.Empty, [], [], 0);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureSettingsAsync(connection, cancellationToken).ConfigureAwait(false);

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        var writes = 0;
        var added = new List<string>();
        var updated = new List<string>();

        var existing = new Dictionary<string, (string Title, string Regime, string Authority, string Frequency, long LeadDays, string DocRequirements, long IsSeed)>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `code`,`title`,`regime`,`authority`,`frequency`,`lead_days`,`doc_requirements`,`is_seed` FROM `epc_bos_compliance_obligations`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                existing[Convert.ToString(reader.GetValue(0)) ?? string.Empty] = (
                    Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                    Convert.ToString(reader.GetValue(2)) ?? string.Empty,
                    Convert.ToString(reader.GetValue(3)) ?? string.Empty,
                    Convert.ToString(reader.GetValue(4)) ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(5)),
                    Convert.ToString(reader.GetValue(6)) ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(7)));
            }
        }

        foreach (var o in SeedObligations(await CompanyCountryAsync(connection, cancellationToken).ConfigureAwait(false), await DnfbpProfileAsync(connection, cancellationToken).ConfigureAwait(false)))
        {
            if (!existing.TryGetValue(o.Code, out var cur))
            {
                writes += await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT INTO `epc_bos_compliance_obligations` (`code`,`title`,`regime`,`authority`,`frequency`,`lead_days`,`doc_requirements`,`is_seed`,`active`,`time`) VALUES (?,?,?,?,?,?,?,1,1,?)"),
                    cancellationToken,
                    o.Code, o.Title, o.Regime, o.Authority, o.Frequency, o.LeadDays, o.DocRequirements, now).ConfigureAwait(false);
                added.Add(o.Title);
                continue;
            }
            if (cur.IsSeed != 1) continue; // admin-owned row — never overwrite
            var changed = cur.Title != o.Title || cur.Authority != o.Authority || cur.Frequency != o.Frequency
                || cur.LeadDays != o.LeadDays || cur.DocRequirements != o.DocRequirements || cur.Regime != o.Regime;
            if (changed)
            {
                writes += await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_bos_compliance_obligations` SET `title`=?, `regime`=?, `authority`=?, `frequency`=?, `lead_days`=?, `doc_requirements`=?, `time`=? WHERE `code`=? AND `is_seed`=1"),
                    cancellationToken,
                    o.Title, o.Regime, o.Authority, o.Frequency, o.LeadDays, o.DocRequirements, now, o.Code).ConfigureAwait(false);
                updated.Add(o.Title);
            }
        }

        var existingRet = new Dictionary<string, (string Label, long Years, string Basis, string LegalRef, long IsSeed)>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `doc_type`,`label`,`retention_years`,`basis`,`legal_ref`,`is_seed` FROM `epc_bos_retention_rules`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                existingRet[Convert.ToString(reader.GetValue(0)) ?? string.Empty] = (
                    Convert.ToString(reader.GetValue(1)) ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(2)),
                    Convert.ToString(reader.GetValue(3)) ?? string.Empty,
                    Convert.ToString(reader.GetValue(4)) ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(5)));
            }
        }

        foreach (var r in SeedRetention(await CompanyCountryAsync(connection, cancellationToken).ConfigureAwait(false), await DnfbpProfileAsync(connection, cancellationToken).ConfigureAwait(false)))
        {
            if (!existingRet.TryGetValue(r.DocType, out var cur))
            {
                writes += await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT INTO `epc_bos_retention_rules` (`doc_type`,`label`,`retention_years`,`basis`,`legal_ref`,`is_seed`,`active`,`time`) VALUES (?,?,?,?,?,1,1,?)"),
                    cancellationToken,
                    r.DocType, r.Label, r.RetentionYears, r.Basis, r.LegalRef, now).ConfigureAwait(false);
                added.Add(r.Label + " (retention)");
                continue;
            }
            if (cur.IsSeed != 1) continue;
            var changed = cur.Label != r.Label || cur.Years != r.RetentionYears || cur.Basis != r.Basis || cur.LegalRef != r.LegalRef;
            if (changed)
            {
                writes += await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_bos_retention_rules` SET `label`=?, `retention_years`=?, `basis`=?, `legal_ref`=?, `time`=? WHERE `doc_type`=? AND `is_seed`=1"),
                    cancellationToken,
                    r.Label, r.RetentionYears, r.Basis, r.LegalRef, now, r.DocType).ConfigureAwait(false);
                updated.Add(r.Label + " (retention)");
            }
        }

        writes += await SetSettingAsync(connection, "bos_compliance_catalog_version", CatalogVersion, cancellationToken).ConfigureAwait(false);
        writes += await SetSettingAsync(connection, "bos_compliance_last_fetch", now.ToString(), cancellationToken).ConfigureAwait(false);

        // ajax_erp.php bos_compliance_fetch message — verbatim.
        string message;
        if (added.Count == 0 && updated.Count == 0)
        {
            message = $"Compliance catalog is up to date — no changes (version {CatalogVersion}).";
        }
        else
        {
            var parts = new List<string>();
            if (added.Count > 0)
            {
                parts.Add($"{added.Count} added ({string.Join(", ", added.Take(6))}{(added.Count > 6 ? "…" : "")})");
            }
            if (updated.Count > 0)
            {
                parts.Add($"{updated.Count} updated ({string.Join(", ", updated.Take(6))}{(updated.Count > 6 ? "…" : "")})");
            }
            message = $"Fetched catalog {CatalogVersion} — {string.Join("; ", parts)}.";
        }

        return new(true, message, CatalogVersion, added, updated, writes);
    }

    private async Task<string> CompanyCountryAsync(DbConnection connection, CancellationToken ct)
    {
        var c = (await GetSettingAsync(connection, "erp_company_country", ct).ConfigureAwait(false)).Trim();
        return c == string.Empty ? "AE" : c.ToUpperInvariant();
    }

    /// <summary>PHP <c>epc_bos_compliance_dnfbp_profile</c>.</summary>
    private async Task<(bool IsDnfbp, bool IsPrecious)> DnfbpProfileAsync(DbConnection connection, CancellationToken ct)
    {
        var blob = string.Join(' ', new[]
        {
            (await GetSettingAsync(connection, "erp_industry_pack", ct).ConfigureAwait(false)).Trim(),
            (await GetSettingAsync(connection, "erp_industry_profile", ct).ConfigureAwait(false)).Trim(),
        }.Where(s => s.Length > 0)).ToLowerInvariant();
        var precious = PreciousNeedles.Any(n => blob.Contains(n, StringComparison.Ordinal));
        var dnfbp = DnfbpNeedles.Any(n => blob.Contains(n, StringComparison.Ordinal));
        return (dnfbp, precious);
    }

    /// <summary>PHP <c>epc_bos_compliance_seed_obligations</c>.</summary>
    private static List<Obligation> SeedObligations(string country, (bool IsDnfbp, bool IsPrecious) dnfbp)
    {
        var common = new List<Obligation>
        {
            new("einvoice", "E-invoicing transmission", "e-invoicing", "Tax authority / Peppol", "monthly", 5, "Issued sales invoices in approved XML/JSON; clearance/reporting receipts."),
            new("payroll", "Payroll / wage protection run", "labour", "Labour / WPS", "monthly", 10, "Salary register, bank/WPS file, employee acknowledgements."),
        };
        List<Obligation> region;
        if (country == "AE")
        {
            region = new List<Obligation>
            {
                new("vat_return", "VAT return (FTA)", "VAT", "UAE FTA (EmaraTax)", "quarterly", 28, "Sales/purchase ledgers, output & input VAT summary, adjustments."),
                new("corporate_tax", "Corporate Tax return", "corporate-tax", "UAE FTA", "annual", 270, "Financial statements, tax computation, transfer-pricing disclosures."),
                new("esr", "Economic Substance notification", "ESR", "Ministry of Finance", "annual", 180, "Relevant-activity assessment, substance evidence."),
            };
        }
        else if (country == "SA")
        {
            region = new List<Obligation>
            {
                new("vat_return", "VAT return (ZATCA)", "VAT", "ZATCA (Saudi Arabia)", "monthly", 30, "Sales/purchase ledgers, output & input VAT summary."),
                new("corporate_tax", "Zakat / Corporate income tax return", "corporate-tax", "ZATCA (Saudi Arabia)", "annual", 120, "Financial statements, zakat/tax computation."),
            };
        }
        else
        {
            region = new List<Obligation>
            {
                new("vat_return", "VAT / GST return", "VAT", "Tax authority", "quarterly", 28, "Sales/purchase ledgers, output & input tax summary."),
                new("corporate_tax", "Corporate income tax return", "corporate-tax", "Tax authority", "annual", 270, "Financial statements, tax computation."),
            };
        }

        var aml = new List<Obligation>();
        if (dnfbp.IsDnfbp)
        {
            aml = AmlObligations(country, dnfbp.IsPrecious);
        }
        region.AddRange(aml);
        region.AddRange(common);
        return region;
    }

    /// <summary>PHP <c>epc_bos_compliance_aml_obligations</c>.</summary>
    private static List<Obligation> AmlObligations(string country, bool isPrecious)
    {
        var fiu = country == "AE" ? "UAE FIU (goAML) / MoET" : "Financial Intelligence Unit (FIU)";
        var sup = country == "AE" ? "Ministry of Economy & Tourism (MoET)" : "AML supervisory authority";
        var output = new List<Obligation>
        {
            new("aml_goaml", "AML — goAML / FIU registration & renewal", "AML", fiu, "annual", 30, "goAML enrolment, supervisory registration, trade-licence & activity details, MLRO appointment."),
            new("aml_risk_assessment", "AML — enterprise-wide risk assessment", "AML", sup, "annual", 60, "Business risk assessment across customer, geography, product/service and channel risk; risk-mitigation plan."),
            new("aml_monitoring", "AML — transaction monitoring, sanctions/PEP screening & STR/SAR", "AML", fiu, "monthly", 15, "Monitoring log, UN/local sanctions & PEP screening hits, suspicious-transaction (STR/SAR) filings where applicable."),
            new("aml_training", "AML — staff training & awareness", "AML", sup, "annual", 30, "Training plan, attendance records, competency assessment."),
            new("aml_independent_audit", "AML — independent AML/CFT audit", "AML", sup, "annual", 90, "Independent review of the AML/CFT programme effectiveness and remediation plan."),
        };
        if (isPrecious)
        {
            var thr = country == "AE" ? "AED 55,000" : "the local cash threshold";
            output.Add(new Obligation("aml_dpmsr", "AML — DPMS cash/precious-metals report (≥ " + thr + ")", "AML", fiu, "monthly", 15, "Dealers-in-Precious-Metals-and-Stones report (DPMSR) for cash transactions ≥ " + thr + "; responsible-sourcing / gold supply-chain due diligence."));
        }
        return output;
    }

    /// <summary>PHP <c>epc_bos_compliance_seed_retention</c>.</summary>
    private static List<Retention> SeedRetention(string country, (bool IsDnfbp, bool IsPrecious) dnfbp)
    {
        var invY = country == "AE" ? 5 : 6;
        const int ctY = 7;
        var rules = new List<Retention>
        {
            new("tax_invoice", "Tax invoices (issued & received)", invY, "From end of tax period", country == "AE" ? "UAE VAT Law" : "Local VAT law"),
            new("accounting_records", "Accounting books & records", ctY, "From end of financial year", country == "AE" ? "UAE CT Law" : "Companies law"),
            new("payroll", "Payroll & employee records", 5, "From end of employment", "Labour law"),
            new("customs", "Customs / import-export docs", 5, "From declaration date", "Customs law"),
            new("contracts", "Contracts & agreements", 7, "From contract end", "General"),
        };
        if (dnfbp.IsDnfbp)
        {
            var amlRef = country == "AE" ? "UAE AML Decree-Law 10/2025" : "Local AML law";
            rules.Add(new Retention("aml_cdd", "AML / CDD & KYC records", 5, "From end of business relationship or transaction date", amlRef));
            rules.Add(new Retention("aml_str", "AML — STR/SAR & monitoring records", 5, "From date of report / transaction", amlRef));
        }
        return rules;
    }

    /// <summary>PHP <c>epc_erp_adv_get_setting</c> (fail-closed default on error → empty).</summary>
    private static async Task<string> GetSettingAsync(DbConnection connection, string key, CancellationToken ct)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1");
            ErpDb.AddParameters(command, key);
            var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return value is null or DBNull ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static async Task<int> SetSettingAsync(DbConnection connection, string key, string value, CancellationToken ct)
    {
        return await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional("INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            ct,
            key,
            value).ConfigureAwait(false);
    }

    private static async Task EnsureSettingsAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_price_settings` (
                `setting_key` varchar(128) NOT NULL,
                `setting_value` text,
                PRIMARY KEY (`setting_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_bos_compliance_ensure_schema</c> — verbatim.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_bos_compliance_obligations` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`code` varchar(48) NOT NULL,
		`title` varchar(160) NOT NULL,
		`regime` varchar(64) NOT NULL DEFAULT 'general',
		`authority` varchar(120) DEFAULT NULL,
		`frequency` enum('monthly','quarterly','annual','one_off') NOT NULL DEFAULT 'monthly',
		`lead_days` int(11) NOT NULL DEFAULT 28,
		`doc_requirements` text,
		`notes` text,
		`is_seed` tinyint(1) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `u_code` (`code`),
		KEY `x_regime` (`regime`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS compliance obligations';", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_bos_compliance_filings` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`obligation_id` int(11) NOT NULL DEFAULT 0,
		`period_label` varchar(48) NOT NULL,
		`period_start` int(11) NOT NULL DEFAULT 0,
		`period_end` int(11) NOT NULL DEFAULT 0,
		`due_date` int(11) NOT NULL DEFAULT 0,
		`status` enum('open','filed','waived') NOT NULL DEFAULT 'open',
		`filed_at` int(11) NOT NULL DEFAULT 0,
		`reference` varchar(120) DEFAULT NULL,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `u_period` (`obligation_id`,`period_label`),
		KEY `x_due` (`due_date`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS compliance filing calendar';", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_bos_retention_rules` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`doc_type` varchar(64) NOT NULL,
		`label` varchar(160) NOT NULL,
		`retention_years` int(11) NOT NULL DEFAULT 5,
		`basis` varchar(160) DEFAULT NULL,
		`legal_ref` varchar(160) DEFAULT NULL,
		`is_seed` tinyint(1) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `u_doc` (`doc_type`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS document retention rules';", ct).ConfigureAwait(false);
    }
}
