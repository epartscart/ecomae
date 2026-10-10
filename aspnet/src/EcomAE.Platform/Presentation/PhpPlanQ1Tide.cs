using System.Globalization;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-tide tenant country profile. PHP identifiers kept for the inventory:
/// <c>epc_tenant_country_normalize</c>, <c>epc_tenant_country_erp_defaults</c>,
/// <c>epc_tenant_country_pricing_set</c>, <c>epc_tenant_country_tenant_pdo</c>,
/// <c>epc_tenant_apply_country_profile</c>, <c>epc_tenant_country_market_label</c>.
/// Path: <c>content/shop/tenant_hub/epc_tenant_country_profile.php</c>.
/// GET never mints a session cookie. Leftover tax-toolkit / APAI stay injected.
/// Do not write those leftover unique basenames.
/// </summary>
public static class PhpPlanQ1Tide
{
    public const string TenantCountryProfilePath = "content/shop/tenant_hub/epc_tenant_country_profile.php";

    public static Func<Dictionary<string, string>>? Countries { get; set; }
    public static Func<string, string>? TaxNameToIso { get; set; }
    public static Func<string, Dictionary<string, object?>>? ApaiCountryMeta { get; set; }
    public static Action<MySqlConnection>? DbEnsure { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>?>? TenantGet { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? LoadSettings { get; set; }
    public static Action<MySqlConnection, Dictionary<string, object?>>? SaveSettings { get; set; }
    public static Func<string, string>? ApaiTenantCountry { get; set; }
    public static Func<long>? Clock { get; set; }
    public static List<Dictionary<string, object?>> SavedSettings { get; } = [];

    public static void Reset()
    {
        Countries = null;
        TaxNameToIso = null;
        ApaiCountryMeta = null;
        DbEnsure = null;
        TenantGet = null;
        LoadSettings = null;
        SaveSettings = null;
        ApaiTenantCountry = null;
        Clock = null;
        SavedSettings.Clear();
    }

    public static string EpcTenantCountryNormalize(string value)
    {
        value = value.Trim().ToUpperInvariant();
        if (Regex.IsMatch(value, "^[A-Z]{2}$"))
        {
            return value;
        }

        var iso = TaxNameToIso?.Invoke(value) ?? (value == "UAE" ? "AE" : "");
        if (iso != "")
        {
            return iso.ToUpperInvariant();
        }

        foreach (var (code, name) in IsoCountries())
        {
            if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }

        return "";
    }

    public static Dictionary<string, object?> EpcTenantCountryErpDefaults(string countryCode)
    {
        var meta = ApaiMeta(countryCode);
        var currency = Str(meta.GetValueOrDefault("currency"), "AED");
        var dateFormat = countryCode == "US" ? "m/d/Y" : "d/m/Y";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["company_country_code"] = countryCode,
            ["company_currency"] = currency,
            ["erp_date_format"] = dateFormat
        };
    }

    public static void EpcTenantCountryPricingSet(MySqlConnection pdo, string key, string value)
    {
        try
        {
            using var cmd = pdo.CreateCommand();
            cmd.CommandText = "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (@k, @v) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // leftover swallows
        }
    }

    public static Dictionary<string, object?> EpcTenantApplyCountryProfile(string siteKey, string countryCode, MySqlConnection? platformPdo)
    {
        siteKey = Regex.Replace(siteKey.Trim().ToLowerInvariant(), "[^a-z0-9_]", "");
        countryCode = EpcTenantCountryNormalize(countryCode);
        var countries = IsoCountries();
        if (siteKey == "" || countryCode == "" || !countries.ContainsKey(countryCode))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["country_code"] = countryCode,
                ["country_name"] = "",
                ["steps"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                ["errors"] = new List<string> { "Invalid site key or country code" }
            };
        }

        var countryName = countries[countryCode];
        var steps = new Dictionary<string, object?>(StringComparer.Ordinal);
        var errors = new List<string>();
        if (platformPdo is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["country_code"] = countryCode,
                ["country_name"] = countryName,
                ["steps"] = steps,
                ["errors"] = new List<string> { "Platform database unavailable" }
            };
        }

        DbEnsure?.Invoke(platformPdo);
        try
        {
            using var upd = platformPdo.CreateCommand();
            upd.CommandText = "UPDATE `epc_portal_tenants` SET `country_code` = @c, `updated_at` = @t WHERE `site_key` = @k";
            upd.Parameters.AddWithValue("@c", countryCode);
            upd.Parameters.AddWithValue("@t", Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            upd.Parameters.AddWithValue("@k", siteKey);
            upd.ExecuteNonQuery();
            steps["registry"] = countryCode;
        }
        catch (Exception ex)
        {
            errors.Add("registry: " + ex.Message);
        }

        var tenantRow = TenantGet?.Invoke(platformPdo, siteKey) ?? FetchTenant(platformPdo, siteKey);
        var hostname = tenantRow is null ? "" : Str(tenantRow.GetValueOrDefault("hostname"));
        if (hostname != "")
        {
            try
            {
                var settings = LoadSettings?.Invoke(platformPdo, hostname)
                    ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["host"] = hostname, ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal) };
                settings["host"] = hostname;
                settings["country_code"] = countryCode;
                var contact = settings.TryGetValue("contact", out var c) && c is Dictionary<string, object?> map
                    ? map
                    : new Dictionary<string, object?>(StringComparer.Ordinal);
                contact["country"] = countryName;
                contact["country_code"] = countryCode;
                settings["contact"] = contact;
                SavedSettings.Add(settings);
                SaveSettings?.Invoke(platformPdo, settings);
                steps["platform_site_settings"] = hostname;
            }
            catch (Exception ex)
            {
                errors.Add("platform_site_settings: " + ex.Message);
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = errors.Count == 0,
            ["country_code"] = countryCode,
            ["country_name"] = countryName,
            ["steps"] = steps,
            ["errors"] = errors
        };
    }

    public static string EpcTenantCountryMarketLabel(MySqlConnection? pdo, string siteKey = "")
    {
        if (ApaiTenantCountry is not null && ApaiCountryMeta is not null)
        {
            var cc = ApaiTenantCountry(siteKey);
            return Str(ApaiMeta(cc).GetValueOrDefault("label"), cc);
        }

        return "United Arab Emirates";
    }

    private static Dictionary<string, string> IsoCountries()
        => Countries?.Invoke() ?? new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AE"] = "United Arab Emirates",
            ["PK"] = "Pakistan",
            ["US"] = "United States",
            ["GB"] = "United Kingdom",
            ["IN"] = "India"
        };

    private static Dictionary<string, object?> ApaiMeta(string countryCode)
    {
        if (ApaiCountryMeta is not null)
        {
            return ApaiCountryMeta(countryCode);
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["US"] = "USD",
            ["GB"] = "GBP",
            ["PK"] = "PKR",
            ["IN"] = "INR"
        };
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["currency"] = map.GetValueOrDefault(countryCode, "AED"),
            ["label"] = countryCode == "PK" ? "Pakistan" : countryCode == "US" ? "United States" : "United Arab Emirates"
        };
    }

    private static Dictionary<string, object?>? FetchTenant(MySqlConnection pdo, string siteKey)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = "SELECT * FROM `epc_portal_tenants` WHERE `site_key` = @k LIMIT 1";
        cmd.Parameters.AddWithValue("@k", siteKey);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
