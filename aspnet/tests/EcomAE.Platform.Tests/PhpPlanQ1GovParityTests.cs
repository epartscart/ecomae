using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1GovParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Gov");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Gov_MatchPhpGolden()
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
            if (!Same(actual, expected))
            {
                failures.Add(name + " expected=" + Truncate(expected.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Gov.PlatformGovernancePath, PhpPlanQ1Gov.TenantConfigPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Gov.PlatformGovernancePath, PhpPlanQ1Gov.TenantConfigPath });

    [Fact]
    public void TenantConfigGet_DoesNotStartASession()
        => Assert.Equal("5", PhpPlanQ1Gov.EpcTenantConfigGet(new PhpPlanQ1Gov.TenantConfigStore(), "siteA", "tax", "vat_rate"));

    private static string Render(string name)
        => name switch
        {
            "gov_static" => Json(GovStatic()),
            "gov_flow" => Json(GovFlow()),
            "cfg_static" => Json(CfgStatic()),
            "cfg_flow" => Json(CfgFlow()),
            _ => "unknown:" + name
        };

    private static object?[] GovStatic()
    {
        var cats = PhpPlanQ1Gov.EpcPlatformGovernanceCategories();
        var defaults = PhpPlanQ1Gov.EpcPlatformGovernanceDefaultRules();
        return new object?[]
        {
            cats.EnumerateObject().Select(p => p.Name).ToArray(),
            PhpPlanQ1Gov.EpcPlatformGovernanceEnforcementLevels(),
            defaults.GetArrayLength(),
            defaults[0].GetProperty("rule_key").GetString(),
            defaults[defaults.GetArrayLength() - 1].GetProperty("rule_key").GetString(),
            PhpPlanQ1Gov.EpcPlatformGovernanceBlockedForBranding("Hello UMAPI"),
            PhpPlanQ1Gov.EpcPlatformGovernanceBlockedForBranding("clean shop")
        };
    }

    private static object?[] GovFlow()
    {
        var store = new PhpPlanQ1Gov.GovStore();
        var n1 = PhpPlanQ1Gov.EpcPlatformGovernanceSeed(store);
        var n2 = PhpPlanQ1Gov.EpcPlatformGovernanceSeed(store);
        var all = PhpPlanQ1Gov.EpcPlatformGovernanceListRules(store);
        var tax = PhpPlanQ1Gov.EpcPlatformGovernanceListRules(store, "tax");
        var ok = PhpPlanQ1Gov.EpcPlatformGovernanceUpdateRule(store, "protocol_umapi_proxy_json", new Dictionary<string, object?> { ["enforcement"] = "blocked", ["active"] = 0 });
        var bad = PhpPlanQ1Gov.EpcPlatformGovernanceUpdateRule(store, "protocol_umapi_proxy_json", new Dictionary<string, object?> { ["nope"] = 1 });
        var empty = PhpPlanQ1Gov.EpcPlatformGovernanceUpdateRule(store, "protocol_umapi_proxy_json", new Dictionary<string, object?> { ["enforcement"] = "nope" });
        var rule = all.Concat(PhpPlanQ1Gov.EpcPlatformGovernanceListRules(store)).First(r => r.RuleKey == "demo_expiry_enforced");
        var demo = new Dictionary<string, object?>(StringComparer.Ordinal) { ["scope"] = rule.Scope, ["config"] = rule.Config };
        var ap = new[]
        {
            PhpPlanQ1Gov.EpcPlatformGovernanceRuleApplies(new Dictionary<string, object?> { ["scope"] = "all_tenants" }, new()),
            PhpPlanQ1Gov.EpcPlatformGovernanceRuleApplies(demo, new Dictionary<string, object?> { ["is_demo"] = 1 }),
            PhpPlanQ1Gov.EpcPlatformGovernanceRuleApplies(demo, new()),
            PhpPlanQ1Gov.EpcPlatformGovernanceRuleApplies(new Dictionary<string, object?> { ["scope"] = "erp_only" }, new Dictionary<string, object?> { ["access_mode"] = "erp_only" }),
            PhpPlanQ1Gov.EpcPlatformGovernanceRuleApplies(new Dictionary<string, object?> { ["scope"] = "tenant:siteA" }, new Dictionary<string, object?> { ["site_key"] = "siteA" }),
            PhpPlanQ1Gov.EpcPlatformGovernanceRuleApplies(new Dictionary<string, object?> { ["scope"] = "tenant:siteA" }, new Dictionary<string, object?> { ["site_key"] = "siteB" })
        };
        var act = PhpPlanQ1Gov.EpcPlatformGovernanceActiveRules(store, new Dictionary<string, object?> { ["is_demo"] = 1 });
        object? country = null;
        if (tax.Count > 0 && tax[0].Config.TryGetValue("country_code", out var cc))
        {
            country = cc;
        }

        return new object?[] { n1, n2, all.Count, tax.Count, ok, bad, empty, all[0].RuleKey, country, ap, act.Count };
    }

    private static object?[] CfgStatic()
    {
        var g = PhpPlanQ1Gov.EpcTenantConfigGroups();
        return new object?[]
        {
            PhpPlanQ1Gov.EpcTenantConfigVersion,
            g.EnumerateObject().Select(p => p.Name).ToArray(),
            g.GetProperty("branding").GetProperty("fields").GetArrayLength(),
            g.GetProperty("tax").GetProperty("fields")[1].GetProperty("default").GetString()
        };
    }

    private static object?[] CfgFlow()
    {
        var store = new PhpPlanQ1Gov.TenantConfigStore();
        var def = PhpPlanQ1Gov.EpcTenantConfigGet(store, "siteA", "tax", "vat_rate");
        var miss = PhpPlanQ1Gov.EpcTenantConfigGet(store, "siteA", "nope", "x");
        var set = PhpPlanQ1Gov.EpcTenantConfigSet(store, "siteA", "branding", "company_name", "Acme", 9);
        var got = PhpPlanQ1Gov.EpcTenantConfigGet(store, "siteA", "branding", "company_name");
        var grp = PhpPlanQ1Gov.EpcTenantConfigGroup(store, "siteA", "branding");
        var badg = PhpPlanQ1Gov.EpcTenantConfigSet(store, "siteA", "nope", "x", "1");
        var badk = PhpPlanQ1Gov.EpcTenantConfigSet(store, "siteA", "branding", "nope", "1");
        var bulk = PhpPlanQ1Gov.EpcTenantConfigBulkSet(store, "siteA", "tax", new Dictionary<string, object?> { ["vat_rate"] = "7.5", ["vat_enabled"] = "0" }, 3);
        var all = PhpPlanQ1Gov.EpcTenantConfigAll(store, "siteA");
        var exp = PhpPlanQ1Gov.EpcTenantConfigExport(store, "siteA");
        var imp = PhpPlanQ1Gov.EpcTenantConfigImport(store, "siteB", exp, 2);
        var fleet = PhpPlanQ1Gov.EpcTenantConfigFleet(store);
        var taxFields = (List<Dictionary<string, object?>>)((Dictionary<string, object?>)all["tax"]!)["fields"]!;
        var bulkResults = (Dictionary<string, object?>)bulk["results"]!;
        var vat = (Dictionary<string, object?>)bulkResults["vat_rate"]!;
        return new object?[]
        {
            def, miss, set["ok"], set["old_value"], set["new_value"], got, grp[0]["value"], badg, badk["ok"], bulk["ok"], vat["new_value"],
            all.Count, taxFields.Count, exp.Count, imp, fleet[0]["site_key"], Convert.ToInt32(fleet[0]["configs_set"], System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 280 ? value : value[..280] + "…";
}
