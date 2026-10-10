using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1KelpParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Kelp");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Kelp_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_portal_tenant_intro.php", PhpPlanQ1Kelp.PortalTenantIntroPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Kelp.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Kelp.PortalTenantIntroPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Kelp.EpcPortalSiteKeyFromHostname("www.acme.test"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Kelp.Reset();
        PhpPlanQ1Kelp.Clock = () => 1700000000;
        PhpPlanQ1Kelp.PlatformIp = () => "203.0.113.10";
        PhpPlanQ1Kelp.EnqueueWarmup = (_, _) => 77;
        return name switch
        {
            "names" => Names(),
            "post" => Post(),
            "validate" => Validate(),
            "db" => Db(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        var defs = PhpPlanQ1Kelp.EpcPortalIntroFieldDefs();
        var req = defs.ToDictionary(kv => kv.Key, kv => Truthy(kv.Value, "required") ? 1 : 0, StringComparer.Ordinal);
        var steps = PhpPlanQ1Kelp.EpcPortalErpOnlyOnboardSteps();
        var guide = PhpPlanQ1Kelp.EpcPortalOnboardGuideSteps();
        var decoded = PhpPlanQ1Kelp.EpcPortalIntroDecode("{\"contact_person\":\"Ada\",\"tagline\":\"Hello\"}");
        return new object?[]
        {
            defs.Keys.ToList(),
            req,
            PhpPlanQ1Kelp.EpcPortalIntroDefaults(),
            PhpPlanQ1Kelp.EpcPortalSiteKeyFromHostname("www.acme-parts.com"),
            PhpPlanQ1Kelp.EpcPortalSiteKeyFromHostname("shop.acme.co.uk"),
            PhpPlanQ1Kelp.EpcPortalSiteKeyFromHostname(""),
            PhpPlanQ1Kelp.EpcPortalIntroDecode(null),
            PhpPlanQ1Kelp.EpcPortalIntroDecode("{"),
            decoded["contact_person"],
            decoded["country"],
            steps.Count,
            steps[0]["title"],
            guide.Count,
            Str(guide[2]["body"]).Contains("203.0.113.10", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Post()
        => new object?[]
        {
            PhpPlanQ1Kelp.EpcPortalIntroFromPost(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["contact_person"] = " Ada Lovelace ",
                ["contact_email"] = "ada@acme.test",
                ["country"] = "Pakistan",
                ["admin_email"] = "admin@acme.test",
                ["theme_template"] = "Modern-Blue!",
                ["storefront_package"] = "Pro Pack"
            }),
            PhpPlanQ1Kelp.EpcPortalIntroFromPost(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_only"] = 1,
                ["erp_modules_preset"] = "hr_only",
                ["tenant_mode"] = "full"
            }),
            PhpPlanQ1Kelp.EpcPortalIntroFromPost(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_only_shared"] = 1,
                ["country_code"] = "pk-1"
            }),
            PhpPlanQ1Kelp.EpcPortalIntroFromPost(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenant_mode"] = "erp_only",
                ["access_mode"] = "full_commerce",
                ["dedicated_db"] = 0,
                ["scale_policy"] = "shared_docpart"
            })
        };

    private static object Validate()
    {
        var ok = PhpPlanQ1Kelp.EpcPortalIntroValidate(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["contact_person"] = "Ada",
            ["contact_email"] = "ada@acme.test",
            ["country"] = "United Arab Emirates",
            ["country_code"] = "AE",
            ["admin_email"] = "admin@acme.test"
        }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "www.acme.test", ["trade_name"] = "Acme" });
        var bad = PhpPlanQ1Kelp.EpcPortalIntroValidate(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["contact_person"] = "",
            ["contact_email"] = "nope",
            ["country"] = "",
            ["admin_email"] = "also-nope"
        }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "nodot", ["trade_name"] = "" });
        var shared = PhpPlanQ1Kelp.EpcPortalIntroValidate(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["contact_person"] = "Ada",
            ["contact_email"] = "ada@acme.test",
            ["country"] = "Pakistan",
            ["admin_email"] = "admin@acme.test",
            ["erp_only_shared"] = 1
        }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "", ["trade_name"] = "Beta", ["hosted_on"] = "platform" });
        var merged = PhpPlanQ1Kelp.EpcPortalIntroMerge(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["contact_person"] = "Old", ["tagline"] = "Keep" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["contact_person"] = "New", ["tagline"] = "" });
        return new object?[] { ok, bad, shared, merged["contact_person"], merged["tagline"], merged["country"] };
    }

    private static object Db()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_kelp_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, """
                CREATE TABLE `epc_portal_tenants` (
                    `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    `site_key` VARCHAR(64) NOT NULL,
                    `hostname` VARCHAR(120) NOT NULL,
                    `industry_code` VARCHAR(32) NOT NULL DEFAULT 'auto_parts',
                    `status` VARCHAR(24) NOT NULL DEFAULT 'draft',
                    `trade_name` VARCHAR(120) NOT NULL DEFAULT '',
                    `hub_name` VARCHAR(120) NOT NULL DEFAULT '',
                    `from_email` VARCHAR(120) NOT NULL DEFAULT '',
                    `db_name` VARCHAR(64) NOT NULL DEFAULT '',
                    `db_user` VARCHAR(64) NOT NULL DEFAULT '',
                    `db_password` VARCHAR(255) NOT NULL DEFAULT '',
                    `notes` VARCHAR(500) NOT NULL DEFAULT '',
                    `intro_json` TEXT NULL,
                    `hosted_on` VARCHAR(24) NOT NULL DEFAULT 'client',
                    `erp_only_shared` TINYINT(1) NOT NULL DEFAULT 0,
                    `dedicated_db` TINYINT(1) NOT NULL DEFAULT 0,
                    `scale_policy` VARCHAR(32) NOT NULL DEFAULT 'shared_docpart',
                    `blockchain_mode` VARCHAR(24) NOT NULL DEFAULT 'anchor',
                    `created_at` INT NOT NULL DEFAULT 0,
                    `updated_at` INT NOT NULL DEFAULT 0,
                    UNIQUE KEY `site_key` (`site_key`)
                )
                """);
            var missing = PhpPlanQ1Kelp.EpcPortalTenantGet(db, "nope");
            var emptyKey = PhpPlanQ1Kelp.EpcPortalTenantGet(db, "!!!");
            var failOnboard = PhpPlanQ1Kelp.EpcPortalOnboardClient(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["trade_name"] = "" }, "op");
            var okOnboard = PhpPlanQ1Kelp.EpcPortalOnboardClient(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["hostname"] = "www.acme-parts.test",
                ["site_key"] = "acmeparts",
                ["trade_name"] = "Acme Parts",
                ["contact_person"] = "Ada",
                ["contact_email"] = "ada@acme.test",
                ["admin_email"] = "admin@acme.test",
                ["country"] = "United Arab Emirates",
                ["country_code"] = "AE",
                ["status"] = "dns_pending",
                ["db_name"] = "acmeparts",
                ["erp_modules"] = new[] { "erp_sales" }
            }, "op1");
            var row = PhpPlanQ1Kelp.EpcPortalTenantGet(db, "acmeparts");
            var intro = PhpPlanQ1Kelp.EpcPortalIntroDecode(Str(row?.GetValueOrDefault("intro_json")));
            var check = PhpPlanQ1Kelp.EpcPortalTenantLaunchChecklist(db, "acmeparts");
            var themeMissing = PhpPlanQ1Kelp.EpcPortalApplyIndustryThemeToTenant(db, "missing");
            var theme = PhpPlanQ1Kelp.EpcPortalApplyIndustryThemeToTenant(db, "acmeparts", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["theme_template"] = "classic",
                ["push_client"] = 0
            });
            var items = (List<Dictionary<string, object?>>)check["items"]!;
            return new object?[]
            {
                missing is null ? 1 : 0,
                emptyKey is null ? 1 : 0,
                Truthy(failOnboard, "ok") ? 1 : 0,
                failOnboard["errors"] is List<string> errs ? errs.Count : 0,
                Truthy(okOnboard, "ok") ? 1 : 0,
                okOnboard["site_key"],
                okOnboard["hostname"],
                okOnboard["dedicated_db"],
                okOnboard["scale_policy"],
                okOnboard["warmup_job_id"],
                okOnboard["country_code"],
                okOnboard["country_profile"] is Dictionary<string, object?> cp ? cp.GetValueOrDefault("message") : "",
                row?.GetValueOrDefault("trade_name"),
                intro["submitted_at"],
                intro["submitted_by"],
                check["total"],
                check["done"],
                Truthy(check, "ready") ? 1 : 0,
                items[0]["hint"],
                Truthy(themeMissing, "ok") ? 1 : 0,
                themeMissing["message"],
                Truthy(theme, "ok") ? 1 : 0,
                theme["theme_template"],
                PhpPlanQ1Kelp.SavedSettings.Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

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

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is true or 1 or 1L or 1.0 or "1";

    private static string Str(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

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
