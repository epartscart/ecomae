using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-yard tenant readiness score. PHP identifiers kept for the inventory:
/// <c>epc_readiness_score</c>, <c>epc_readiness_tier_label</c>,
/// <c>epc_readiness_check_isolation</c>, <c>epc_readiness_check_mfa</c>,
/// <c>epc_readiness_check_backup</c>, <c>epc_readiness_check_einvoice</c>,
/// <c>epc_readiness_check_homepage_perf</c>, <c>epc_readiness_check_compliance</c>,
/// <c>epc_readiness_check_erp_modules</c>, <c>epc_readiness_check_branding</c>,
/// <c>epc_readiness_check_webhooks</c>, <c>epc_readiness_fleet_summary</c>.
/// GET never mints a session cookie. Scores stay per <c>site_key</c>.
/// </summary>
public static class PhpPlanQ1Yard
{
    public const string ReadinessPath = "content/general_pages/epc_readiness_score.php";

    public sealed class SettingRow
    {
        public string SiteKey { get; set; } = "";
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public sealed class TenantRow
    {
        public string SiteKey { get; set; } = "";
        public string TradeName { get; set; } = "";
        public string Status { get; set; } = "";
        public string Industry { get; set; } = "";
        public int ErpEnabled { get; set; }
    }

    public sealed class WebhookRow
    {
        public string TenantKey { get; set; } = "";
        public int Active { get; set; }
    }

    public static List<SettingRow> Settings { get; } = [];
    public static List<TenantRow> Tenants { get; } = [];
    public static List<WebhookRow> Webhooks { get; } = [];
    public static bool HasSettings { get; set; } = true;
    public static bool HasTenants { get; set; } = true;
    public static bool HasWebhooks { get; set; } = true;
    public static HashSet<string> TokenSites { get; set; } = new(StringComparer.Ordinal);
    public static Func<long> Clock { get; set; } = () => 1_760_083_200;

    public static void Reset()
    {
        Settings.Clear();
        Tenants.Clear();
        Webhooks.Clear();
        HasSettings = true;
        HasTenants = true;
        HasWebhooks = true;
        TokenSites = new(StringComparer.Ordinal);
        Clock = () => 1_760_083_200;
    }

    public static string EpcReadinessTierLabel(string tier)
        => tier switch
        {
            "demo" => "Demo",
            "pilot" => "Pilot",
            "paid" => "Production",
            "enterprise" => "Enterprise",
            _ => "Unknown"
        };

    public static Dictionary<string, object?> EpcReadinessScore(string siteKey)
    {
        var checks = new List<Dictionary<string, object?>>
        {
            EpcReadinessCheckIsolation(siteKey),
            EpcReadinessCheckMfa(siteKey),
            EpcReadinessCheckBackup(siteKey),
            EpcReadinessCheckEinvoice(siteKey),
            EpcReadinessCheckHomepagePerf(siteKey),
            EpcReadinessCheckCompliance(siteKey),
            EpcReadinessCheckErpModules(siteKey),
            EpcReadinessCheckBranding(siteKey),
            EpcReadinessCheckWebhooks(siteKey)
        };
        var total = checks.Sum(c => PhpInt(c["weight"]));
        var earned = checks.Sum(c => PhpInt(c["earned"]));
        var score = total > 0 ? PhpRound((earned / (double)total) * 100) : 0;
        var tier = score >= 80 ? "enterprise" : score >= 60 ? "paid" : score >= 30 ? "pilot" : "demo";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = siteKey,
            ["score"] = score,
            ["tier"] = tier,
            ["tier_label"] = EpcReadinessTierLabel(tier),
            ["total_weight"] = total,
            ["earned_weight"] = earned,
            ["checks"] = checks,
            ["generated_at"] = "ISO"
        };
    }

    public static Dictionary<string, object?> EpcReadinessCheckIsolation(string siteKey)
    {
        var weight = 20;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var val = Setting(siteKey, "isolation_audit_status");
            if (val is "pass" or "ok")
            {
                status = "pass";
                detail = "Site_key isolation audit passed.";
                earned = weight;
            }
            else if (val == "warn")
            {
                status = "warn";
                detail = "Isolation audit has warnings. Review recommended.";
                earned = (int)(weight * 0.5);
            }
            else if (val == "fail")
            {
                status = "fail";
                detail = "Isolation audit failed. Fix cross-tenant leaks.";
            }
            else
            {
                status = "not_run";
                detail = "Isolation audit not yet run for this tenant.";
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "Audit system not available.";
        }

        return Check("isolation", "Commerce Isolation", weight, earned, status, detail, "fa-shield",
            "Run isolation audit from BOS Fleet Command.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckMfa(string siteKey)
    {
        var weight = 15;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var enabled = Count(siteKey, row => row.Key == "mfa_enabled" && row.Value == "1") > 0;
            if (enabled)
            {
                status = "pass";
                detail = "MFA (TOTP) is enabled for finance roles.";
                earned = weight;
            }
            else
            {
                status = "fail";
                detail = "MFA not enabled. Required for enterprise tier.";
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "MFA check unavailable.";
        }

        return Check("mfa", "MFA Enrollment", weight, earned, status, detail, "fa-lock",
            "Enable MFA in tenant CP security settings.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckBackup(string siteKey)
    {
        var weight = 10;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var lastBackup = PhpInt(Setting(siteKey, "last_backup_time"));
            if (lastBackup <= 0)
            {
                status = "fail";
                detail = "No backup recorded. Set up daily backup schedule.";
            }
            else
            {
                var age = Clock() - lastBackup;
                var ageHours = PhpRound(age / 3600.0);
                if (age < 86400)
                {
                    status = "pass";
                    detail = "Last backup " + ageHours.ToString(CultureInfo.InvariantCulture) + "h ago. Within 24h RPO.";
                    earned = weight;
                }
                else if (age < 172800)
                {
                    status = "warn";
                    detail = "Last backup " + ageHours.ToString(CultureInfo.InvariantCulture) + "h ago. Exceeds 24h RPO.";
                    earned = (int)(weight * 0.5);
                }
                else
                {
                    status = "fail";
                    detail = "Last backup " + PhpRound(age / 86400.0).ToString(CultureInfo.InvariantCulture) + " days ago. Critical.";
                }
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "Backup tracking not configured.";
        }

        return Check("backup", "Backup Age", weight, earned, status, detail, "fa-database",
            "Configure daily backup via cron or CloudPanel.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckEinvoice(string siteKey)
    {
        var weight = 15;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var mode = Setting(siteKey, "einvoice_asp_mode") ?? "";
            if (mode is "api" or "live")
            {
                status = "pass";
                detail = "E-invoice ASP API integration is live.";
                earned = weight;
            }
            else if (mode == "manual")
            {
                status = "warn";
                detail = "E-invoice in manual mode. Switch to API for automation.";
                earned = (int)(weight * 0.5);
            }
            else if (mode == "test")
            {
                status = "warn";
                detail = "E-invoice in test mode. Move to live for FTA compliance.";
                earned = (int)(weight * 0.3);
            }
            else
            {
                status = "fail";
                detail = "E-invoice not configured. Required for UAE FTA compliance.";
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "E-invoice check unavailable.";
        }

        return Check("einvoice", "E-Invoice (FTA)", weight, earned, status, detail, "fa-paper-plane",
            "Configure ASP API keys in ERP Finance settings.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckHomepagePerf(string siteKey)
    {
        var weight = 10;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var loadMs = PhpInt(Setting(siteKey, "homepage_load_ms"));
            if (loadMs <= 0)
            {
                status = "not_run";
                detail = "Homepage load time not measured. Run performance test.";
            }
            else if (loadMs < 2000)
            {
                status = "pass";
                detail = "Homepage loads in " + loadMs.ToString(CultureInfo.InvariantCulture) + "ms. Under 2s target.";
                earned = weight;
            }
            else if (loadMs < 5000)
            {
                status = "warn";
                detail = "Homepage loads in " + loadMs.ToString(CultureInfo.InvariantCulture) + "ms. Target is under 2s.";
                earned = (int)(weight * 0.5);
            }
            else
            {
                status = "fail";
                detail = "Homepage loads in " + loadMs.ToString(CultureInfo.InvariantCulture) + "ms. Critical performance issue.";
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "Performance check unavailable.";
        }

        return Check("homepage_perf", "Homepage Performance", weight, earned, status, detail, "fa-tachometer",
            "Enable page cache, gzip, and optimize queries.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckCompliance(string siteKey)
    {
        var weight = 10;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var checks = 0;
            var passed = 0;
            checks++;
            if (Count(siteKey, row => row.Key == "vat_trn" && row.Value != "") > 0)
            {
                passed++;
            }

            checks++;
            if (Count(siteKey, row => row.Key == "trade_license" && row.Value != "") > 0)
            {
                passed++;
            }

            if (checks == passed && checks > 0)
            {
                status = "pass";
                detail = "VAT TRN and trade license configured.";
                earned = weight;
            }
            else if (passed > 0)
            {
                status = "warn";
                detail = passed.ToString(CultureInfo.InvariantCulture) + "/" + checks.ToString(CultureInfo.InvariantCulture)
                    + " compliance items configured.";
                earned = (int)(weight * (passed / (double)checks));
            }
            else
            {
                status = "fail";
                detail = "No compliance items configured.";
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "Compliance check unavailable.";
        }

        return Check("compliance", "VAT / Compliance", weight, earned, status, detail, "fa-balance-scale",
            "Add VAT TRN and trade license in tenant settings.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckErpModules(string siteKey)
    {
        var weight = 10;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var val = Setting(siteKey, "erp_modules_active") ?? "";
            var modules = val != "" ? DecodeArray(val) : null;
            if (modules is { Count: > 0 })
            {
                var count = modules.Count;
                if (count >= 5)
                {
                    status = "pass";
                    detail = count.ToString(CultureInfo.InvariantCulture) + " ERP modules activated.";
                    earned = weight;
                }
                else
                {
                    status = "warn";
                    detail = count.ToString(CultureInfo.InvariantCulture) + " ERP modules activated. Recommend 5+ for full suite.";
                    earned = (int)(weight * Math.Min(1, count / 5.0));
                }
            }
            else
            {
                RequireTenants();
                var tenant = Tenants.FirstOrDefault(t => t.SiteKey == siteKey);
                var erpEnabled = tenant?.ErpEnabled ?? 0;
                if (erpEnabled != 0)
                {
                    status = "pass";
                    detail = "ERP enabled for tenant (module list not tracked).";
                    earned = weight;
                }
                else
                {
                    status = "fail";
                    detail = "No ERP modules activated for this tenant.";
                }
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "ERP module check unavailable.";
        }

        return Check("erp_modules", "ERP Modules", weight, earned, status, detail, "fa-th-large",
            "Enable ERP modules in Super CP → Tenant Hub.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckBranding(string siteKey)
    {
        var weight = 5;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            var count = Count(siteKey, row => row.Key.StartsWith("brand_", StringComparison.Ordinal) && row.Value != "");
            if (TokenSites.Contains(siteKey))
            {
                count = Math.Max(count, 1);
            }

            if (count >= 3)
            {
                status = "pass";
                detail = "Tenant branding configured (" + count.ToString(CultureInfo.InvariantCulture) + " design tokens).";
                earned = weight;
            }
            else if (count >= 1)
            {
                status = "warn";
                detail = "Partial branding configured. Add logo and colors.";
                earned = (int)(weight * 0.5);
            }
            else
            {
                status = "fail";
                detail = "No tenant branding configured.";
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "Branding check unavailable.";
        }

        return Check("branding", "Branding / Design Tokens", weight, earned, status, detail, "fa-paint-brush",
            "Configure logo, colors, and design tokens in BOS.");
    }

    public static Dictionary<string, object?> EpcReadinessCheckWebhooks(string siteKey)
    {
        var weight = 5;
        var status = "unknown";
        var detail = "";
        var earned = 0;
        try
        {
            RequireWebhooks();
            var count = Webhooks.Count(w => w.TenantKey == siteKey && w.Active == 1);
            if (count > 0)
            {
                status = "pass";
                detail = count.ToString(CultureInfo.InvariantCulture) + " active webhook" + (count > 1 ? "s" : "") + " configured.";
                earned = weight;
            }
            else
            {
                status = "warn";
                detail = "No webhooks configured. Optional but recommended for integrations.";
                earned = 0;
            }
        }
        catch (InvalidOperationException)
        {
            status = "not_run";
            detail = "Webhook system not deployed yet.";
        }

        return Check("webhooks", "Webhooks", weight, earned, status, detail, "fa-plug",
            "Register webhook endpoints in BOS or ERP settings.");
    }

    public static Dictionary<string, object?> EpcReadinessFleetSummary()
    {
        var tenants = new List<Dictionary<string, object?>>();
        try
        {
            RequireTenants();
            foreach (var row in Tenants.Where(t => t.Status == "live").OrderBy(t => t.TradeName, StringComparer.Ordinal))
            {
                var score = EpcReadinessScore(row.SiteKey);
                var checks = (List<Dictionary<string, object?>>)score["checks"]!;
                tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["site_key"] = row.SiteKey,
                    ["trade_name"] = row.TradeName,
                    ["industry"] = row.Industry,
                    ["score"] = score["score"],
                    ["tier"] = score["tier"],
                    ["tier_label"] = score["tier_label"],
                    ["checks"] = checks.Count,
                    ["passed"] = checks.Count(c => Convert.ToString(c["status"], CultureInfo.InvariantCulture) == "pass")
                });
            }
        }
        catch (InvalidOperationException)
        {
        }

        var avg = 0;
        if (tenants.Count > 0)
        {
            avg = PhpRound(tenants.Sum(t => PhpInt(t["score"])) / (double)tenants.Count);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_count"] = tenants.Count,
            ["average_score"] = avg,
            ["tenants"] = tenants,
            ["generated_at"] = "ISO"
        };
    }

    public static Dictionary<string, object?> ScoreIds(Dictionary<string, object?> score)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var check in (List<Dictionary<string, object?>>)score["checks"]!)
        {
            map[Convert.ToString(check["id"], CultureInfo.InvariantCulture)!] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = check["status"],
                ["earned"] = check["earned"],
                ["detail"] = check["detail"]
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = score["site_key"],
            ["score"] = score["score"],
            ["tier"] = score["tier"],
            ["tier_label"] = score["tier_label"],
            ["total_weight"] = score["total_weight"],
            ["earned_weight"] = score["earned_weight"],
            ["generated_at"] = "ISO",
            ["checks"] = map
        };
    }

    public static void AddSetting(string site, string key, string value)
        => Settings.Add(new SettingRow { SiteKey = site, Key = key, Value = value });

    private static Dictionary<string, object?> Check(
        string id, string label, int weight, int earned, string status, string detail, string icon, string remediation)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["label"] = label,
            ["weight"] = weight,
            ["earned"] = earned,
            ["status"] = status,
            ["detail"] = detail,
            ["icon"] = icon,
            ["remediation"] = remediation
        };

    private static string? Setting(string siteKey, string key)
    {
        RequireSettings();
        var row = Settings.FirstOrDefault(s => s.SiteKey == siteKey && s.Key == key);
        return row?.Value;
    }

    private static int Count(string siteKey, Func<SettingRow, bool> pred)
    {
        RequireSettings();
        return Settings.Count(s => s.SiteKey == siteKey && pred(s));
    }

    private static void RequireSettings()
    {
        if (!HasSettings)
        {
            throw new InvalidOperationException("settings");
        }
    }

    private static void RequireTenants()
    {
        if (!HasTenants)
        {
            throw new InvalidOperationException("tenants");
        }
    }

    private static void RequireWebhooks()
    {
        if (!HasWebhooks)
        {
            throw new InvalidOperationException("webhooks");
        }
    }

    private static List<object?>? DecodeArray(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return doc.RootElement.EnumerateArray().Select(e => (object?)e.GetRawText()).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int PhpRound(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (text.Length == 0)
        {
            return 0;
        }

        var n2 = 0;
        var sign = 1;
        var p = 0;
        while (p < text.Length && char.IsWhiteSpace(text[p]))
        {
            p++;
        }

        if (p < text.Length && (text[p] == '+' || text[p] == '-'))
        {
            sign = text[p] == '-' ? -1 : 1;
            p++;
        }

        var any = false;
        while (p < text.Length && char.IsAsciiDigit(text[p]))
        {
            any = true;
            n2 = (n2 * 10) + (text[p] - '0');
            p++;
        }

        return any ? sign * n2 : 0;
    }
}
