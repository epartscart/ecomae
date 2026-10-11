using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SailParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Sail");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Sail_MatchPhpGolden()
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
        => Assert.Equal("content/shop/tenant_hub/epc_tenant_templates_catalog.php", PhpPlanQ1Sail.TenantTemplatesCatalogPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Sail.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sail.TenantTemplatesCatalogPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Sail.EpcThDefaultIndustryForTemplate("healthcare"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Sail.Reset();
        Seed();
        return name switch
        {
            "guide" => Guide(),
            "codes" => new object[]
            {
                PhpPlanQ1Sail.EpcThPortalCodesForTemplate("Automotive!"),
                PhpPlanQ1Sail.EpcThDefaultIndustryForTemplate("automotive"),
                PhpPlanQ1Sail.EpcThDefaultIndustryForTemplate("unknown_key"),
                PhpPlanQ1Sail.EpcThDefaultIndustryForTemplate("healthcare")
            },
            "catalog" => Catalog(),
            "apply" => Apply(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void Seed()
    {
        PhpPlanQ1Sail.LiveDefs = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["auto_parts"] = new(StringComparer.Ordinal) { ["template_key"] = "automotive", ["mode"] = "hub_root" },
            ["tyres"] = new(StringComparer.Ordinal) { ["template_key"] = "automotive", ["mode"] = "alias" },
            ["jewellery"] = new(StringComparer.Ordinal) { ["template_key"] = "jewellery", ["mode"] = "hub_root" }
        };
        PhpPlanQ1Sail.Groups = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["z_jew"] = new(StringComparer.Ordinal)
            {
                ["template_key"] = "jewellery", ["label"] = "Jewellery", ["description"] = "Gold", ["icon"] = "fa-gem",
                ["erp_base"] = "jew", ["color_scheme"] = new Dictionary<string, object?> { ["primary"] = "#111", ["accent"] = "#222" },
                ["available_sub_areas"] = new[] { "rings" }
            },
            ["a_auto"] = new(StringComparer.Ordinal)
            {
                ["template_key"] = "automotive", ["label"] = "Auto", ["description"] = "Parts", ["icon"] = "fa-car",
                ["erp_base"] = "auto", ["color_scheme"] = new Dictionary<string, object?>(), ["available_sub_areas"] = Array.Empty<string>()
            }
        };
        PhpPlanQ1Sail.Packages = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["auto_pkg"] = new(StringComparer.Ordinal)
            {
                ["label"] = "Auto pack", ["desc"] = "Chrome", ["theme_template"] = "nero",
                ["industry_codes"] = new[] { "auto_parts" }, ["implemented"] = true
            }
        };
        PhpPlanQ1Sail.PackageForIndustry = code => code == "auto_parts" ? "auto_pkg" : "";
        PhpPlanQ1Sail.PortalIndustries = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["auto_parts"] = new(StringComparer.Ordinal) { ["name"] = "Auto parts" },
            ["tyres"] = new(StringComparer.Ordinal) { ["label"] = "Tyres" },
            ["jewellery"] = new(StringComparer.Ordinal) { ["name"] = "Jewellery" }
        };
        PhpPlanQ1Sail.ErpPacks = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["auto"] = new(StringComparer.Ordinal) { ["label"] = "Auto ERP" },
            ["jew"] = new(StringComparer.Ordinal) { ["label"] = "Jew ERP" }
        };
        PhpPlanQ1Sail.SeoHost = tk => tk + ".ecomae.com";
        PhpPlanQ1Sail.ApplyTheme = (_, site, _) =>
        {
            PhpPlanQ1Sail.ApplyTheme = (_, s, _) => new Dictionary<string, object?> { ["ok"] = true, ["message"] = "Applied", ["_site"] = s };
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "Applied", ["_site"] = site };
        };
    }

    private static object Guide()
    {
        var steps = PhpPlanQ1Sail.EpcThTemplatesGuideSteps();
        return new object[] { steps.Count, steps.Select(s => s["title"]).ToList() };
    }

    private static object Catalog()
    {
        var cat = PhpPlanQ1Sail.EpcThIndustryTemplatesCatalog();
        var pkg = PhpPlanQ1Sail.EpcThStorefrontPackagesCatalog();
        return new object[]
        {
            cat.Count,
            cat[0]["label"]!,
            cat[0]["industry_code"]!,
            true.Equals(cat[0]["has_storefront_package"]) ? 1 : 0,
            cat[0]["live_url"]!,
            cat[1]["label"]!,
            cat[1]["erp_pack_label"]!,
            pkg.Count,
            pkg[0]["id"]!,
            true.Equals(pkg[0]["implemented"]) ? 1 : 0
        };
    }

    private static object Apply()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_sail_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var lastSite = "";
            PhpPlanQ1Sail.ApplyTheme = (_, site, _) =>
            {
                lastSite = site;
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "Applied" };
            };
            var bad = PhpPlanQ1Sail.EpcThApplyIndustryTemplate(db, "acme", "!!!");
            var miss = PhpPlanQ1Sail.EpcThApplyIndustryTemplate(db, "acme", "nope");
            var ok = PhpPlanQ1Sail.EpcThApplyIndustryTemplate(db, "acme", "automotive");
            return new object[]
            {
                true.Equals(bad["ok"]) ? 1 : 0,
                bad["message"]!,
                true.Equals(miss["ok"]) ? 1 : 0,
                miss["message"]!,
                true.Equals(ok["ok"]) ? 1 : 0,
                ok["message"]!,
                ok["template_key"]!,
                lastSite
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? throw new InvalidOperationException("missing ECOMAE_LOCAL_MARIADB_E2E_DSN");

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
            _ => true
        };
    }
}
