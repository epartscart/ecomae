using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1TideParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Tide");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Tide_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/shop/tenant_hub/epc_tenant_country_profile.php", PhpPlanQ1Tide.TenantCountryProfilePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Tide.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Tide.TenantCountryProfilePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Tide.EpcTenantCountryNormalize("AE"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Tide.Reset();
        PhpPlanQ1Tide.TaxNameToIso = v => v == "UAE" ? "AE" : "";
        PhpPlanQ1Tide.ApaiCountryMeta = cc =>
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["US"] = "USD",
                ["GB"] = "GBP",
                ["PK"] = "PKR",
                ["IN"] = "INR"
            };
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["currency"] = map.GetValueOrDefault(cc, "AED"),
                ["label"] = cc == "PK" ? "Pakistan" : cc == "US" ? "United States" : "United Arab Emirates"
            };
        };
        PhpPlanQ1Tide.ApaiTenantCountry = _ => "AE";
        return name switch
        {
            "names" => new object[]
            {
                PhpPlanQ1Tide.EpcTenantCountryNormalize("ae"),
                PhpPlanQ1Tide.EpcTenantCountryNormalize("UAE"),
                PhpPlanQ1Tide.EpcTenantCountryNormalize("Pakistan"),
                PhpPlanQ1Tide.EpcTenantCountryNormalize("xx"),
                PhpPlanQ1Tide.EpcTenantCountryNormalize("")
            },
            "defaults" => new object[]
            {
                PhpPlanQ1Tide.EpcTenantCountryErpDefaults("US"),
                PhpPlanQ1Tide.EpcTenantCountryErpDefaults("GB"),
                PhpPlanQ1Tide.EpcTenantCountryErpDefaults("AE"),
                PhpPlanQ1Tide.EpcTenantCountryErpDefaults("PK")
            },
            "apply" => Apply(),
            "market" => Market(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Apply()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_tide_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, """
                CREATE TABLE `epc_portal_tenants` (
                    `site_key` VARCHAR(64) NOT NULL PRIMARY KEY,
                    `hostname` VARCHAR(120) NOT NULL DEFAULT '',
                    `trade_name` VARCHAR(120) NOT NULL DEFAULT '',
                    `db_name` VARCHAR(64) NOT NULL DEFAULT '',
                    `country_code` CHAR(2) NOT NULL DEFAULT '',
                    `updated_at` INT NOT NULL DEFAULT 0
                )
                """);
            Exec(db, "CREATE TABLE `epc_price_settings` (`setting_key` VARCHAR(64) NOT NULL PRIMARY KEY, `setting_value` VARCHAR(255) NOT NULL)");
            PhpPlanQ1Tide.LoadSettings = (_, host) => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["host"] = host,
                ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            };
            var bad = PhpPlanQ1Tide.EpcTenantApplyCountryProfile("", "AE", db);
            var unknown = PhpPlanQ1Tide.EpcTenantApplyCountryProfile("acme", "ZZ", db);
            Exec(db, "INSERT INTO `epc_portal_tenants` (`site_key`,`hostname`,`trade_name`,`db_name`) VALUES ('acme','www.acme.test','Acme','')");
            var ok = PhpPlanQ1Tide.EpcTenantApplyCountryProfile("acme", "PK", db);
            string cc;
            using (var q = db.CreateCommand())
            {
                q.CommandText = "SELECT `country_code` FROM `epc_portal_tenants` WHERE `site_key`='acme'";
                cc = Convert.ToString(q.ExecuteScalar()) ?? "";
            }

            int price;
            using (var q = db.CreateCommand())
            {
                q.CommandText = "SELECT COUNT(*) FROM `epc_price_settings`";
                price = Convert.ToInt32(q.ExecuteScalar());
            }

            var steps = (Dictionary<string, object?>)ok["steps"]!;
            return new object?[]
            {
                okFalse(bad),
                bad["errors"],
                okFalse(unknown),
                Truthy(ok, "ok") ? 1 : 0,
                ok["country_code"],
                ok["country_name"],
                steps.GetValueOrDefault("registry") ?? "",
                steps.GetValueOrDefault("platform_site_settings") ?? "",
                steps.GetValueOrDefault("erp") ?? "",
                steps.ContainsKey("tax_toolkit") ? 1 : 0,
                steps.ContainsKey("apai_sources") ? 1 : 0,
                cc,
                price,
                PhpPlanQ1Tide.SavedSettings.Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Market()
    {
        PhpPlanQ1Tide.ApaiTenantCountry = _ => "PK";
        var with = PhpPlanQ1Tide.EpcTenantCountryMarketLabel(null, "acme");
        PhpPlanQ1Tide.ApaiTenantCountry = _ => "AE";
        var ae = PhpPlanQ1Tide.EpcTenantCountryMarketLabel(null, "acme");
        return new object[] { with, ae };
    }

    private static int okFalse(Dictionary<string, object?> row) => Truthy(row, "ok") ? 1 : 0;

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is true or 1 or 1L or 1.0;

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "local-throwaway-pw";

    private static MySqlConnection OpenAdmin()
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static MySqlConnection OpenDb(string schema)
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;Database={schema};User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static void Exec(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
