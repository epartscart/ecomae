using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BrineParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Brine");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Brine_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_portal_erp_modules.php", PhpPlanQ1Brine.PortalErpModulesPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Brine.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Brine.PortalErpModulesPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Brine.EpcPortalIndustryErpModulesPreset("hr_recruitment"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Brine.Reset();
        InjectNav();
        return name switch
        {
            "names" => Names(),
            "normalize" => Normalize(),
            "onboard" => Onboard(),
            "enabled" => Enabled(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void InjectNav()
    {
        PhpPlanQ1Brine.NavAreasConfig = DefaultNav;
        PhpPlanQ1Brine.StaffAllTabs = () => ["dashboard", "staff_all"];
        PhpPlanQ1Brine.FilterCommerceTabs = (tabs, _) => tabs;
    }

    private static Dictionary<string, object?> DefaultNav()
        => new(StringComparer.Ordinal)
        {
            ["overview"] = Area("dashboard", "workflow"),
            ["sales"] = Area("orders", "invoices"),
            ["finance"] = Area("gl", "cash_bank", "procurement_link"),
            ["tax"] = Area("vat"),
            ["setup"] = Area("admin"),
            ["enterprise"] = Area("org"),
            ["common"] = Area("search"),
            ["risk"] = Area("risk")
        };

    private static Dictionary<string, object?> Area(params string[] tabs)
        => new(StringComparer.Ordinal)
        {
            ["tabs"] = tabs.ToDictionary(t => t, _ => (object?)1, StringComparer.Ordinal)
        };

    private static object Names()
    {
        var reg = PhpPlanQ1Brine.EpcPortalErpModulesRegistry();
        var presets = PhpPlanQ1Brine.EpcPortalErpModulesPresets();
        var map = PhpPlanQ1Brine.EpcPortalIndustryErpModulesPresetMap();
        var ui = PhpPlanQ1Brine.EpcPortalErpModulesPresetsUi();
        var first = reg["erp_overview"];
        return new object?[]
        {
            reg.Count,
            reg.Keys.ToList(),
            first["label"],
            first["area"],
            Truthy(first, "default_erp_only") ? 1 : 0,
            Truthy(first, "default_full") ? 1 : 0,
            presets.Keys.ToList(),
            presets["hr_only"]["modules"],
            presets["custom_shipping_only"]["modules"],
            map,
            PhpPlanQ1Brine.EpcPortalIndustryErpModulesPreset("hr_recruitment"),
            PhpPlanQ1Brine.EpcPortalIndustryErpModulesPreset("Nope!"),
            ui.Keys.ToList(),
            PhpPlanQ1Brine.EpcPortalErpModulesDefaultIds("full"),
            PhpPlanQ1Brine.EpcPortalErpModulesDefaultIds("erp_only")
        };
    }

    private static object Normalize()
        => new object?[]
        {
            PhpPlanQ1Brine.EpcPortalErpModulesNormalizeList("[\"erp_sales\",\"erp_finance\",\"nope\"]"),
            PhpPlanQ1Brine.EpcPortalErpModulesNormalizeList("erp_people, ERP_FINANCE , junk"),
            PhpPlanQ1Brine.EpcPortalErpModulesNormalizeList(new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "erp_overview" },
                "erp_sales",
                "ERP-SALES"
            }),
            PhpPlanQ1Brine.EpcPortalErpModulesNormalizeList(null),
            PhpPlanQ1Brine.EpcPortalErpModulesNormalizeList(""),
            PhpPlanQ1Brine.EpcPortalErpModulesNormalizeList(new[] { "erp_enterprise", "erp_enterprise" }),
            PhpPlanQ1Brine.EpcPortalErpModulesFromPost(new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules_preset"] = "hr_only" }),
            PhpPlanQ1Brine.EpcPortalErpModulesFromPost(new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules"] = new[] { "erp_sales", "nope" } }),
            PhpPlanQ1Brine.EpcPortalErpModulesFromPost(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_modules_preset"] = "missing",
                ["erp_modules"] = new[] { "erp_finance" }
            }),
            PhpPlanQ1Brine.EpcPortalErpModulesDetectPreset(["erp_overview", "erp_people", "erp_collaboration"]),
            PhpPlanQ1Brine.EpcPortalErpModulesDetectPreset(["erp_sales"])
        };

    private static object Onboard()
        => new object?[]
        {
            PhpPlanQ1Brine.EpcPortalErpModulesResolveForOnboard(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_modules"] = new[] { "erp_sales", "erp_finance" }
            }, "logistics", "erp_only"),
            PhpPlanQ1Brine.EpcPortalErpModulesResolveForOnboard(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_modules_preset"] = "customs_logistics"
            }),
            PhpPlanQ1Brine.EpcPortalErpModulesResolveForOnboard(new Dictionary<string, object?>(StringComparer.Ordinal), "hr_recruitment"),
            PhpPlanQ1Brine.EpcPortalErpModulesResolveForOnboard(new Dictionary<string, object?>(StringComparer.Ordinal), "unknown_x", "erp_only"),
            PhpPlanQ1Brine.EpcPortalErpModulesResolveForOnboard(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["erp_modules"] = Array.Empty<string>()
            }, "tax_advisory"),
            PhpPlanQ1Brine.EpcPortalErpModulesAreaEnabled("setup", new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules"] = new[] { "erp_sales" } }),
            PhpPlanQ1Brine.EpcPortalErpModulesAreaEnabled("sales", new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules"] = new[] { "erp_sales" } }),
            PhpPlanQ1Brine.EpcPortalErpModulesAreaEnabled("people", new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules"] = new[] { "erp_sales" } })
        };

    private static object Enabled()
    {
        var stored = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales", "erp_people" }
        });
        var json = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules_json"] = "[\"erp_finance\"]"
        });
        var defaults = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal));
        PhpPlanQ1Brine.IsPlatformErpActive = () => true;
        var full = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        PhpPlanQ1Brine.IsPlatformErpActive = () => false;
        PhpPlanQ1Brine.IsPlatformErpRequest = () => true;
        var req = PhpPlanQ1Brine.EpcPortalErpModulesEnabled(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        PhpPlanQ1Brine.IsPlatformErpRequest = () => false;
        var areas = PhpPlanQ1Brine.EpcPortalErpModulesEnabledAreas(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        areas.Sort(StringComparer.Ordinal);
        var tabs = PhpPlanQ1Brine.EpcPortalErpModulesAllowedTabs(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        PhpPlanQ1Brine.FilterCommerceTabs = (list, _) => list.Where(t => t != "gl").ToList();
        var filteredTabs = PhpPlanQ1Brine.EpcPortalErpModulesAllowedTabs(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["erp_modules"] = new[] { "erp_sales" }
        });
        PhpPlanQ1Brine.FilterCommerceTabs = (list, _) => list;
        var intersect = PhpPlanQ1Brine.EpcErpFilterTabsByTenantModules(
            ["orders", "gl", "nope"],
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules"] = new[] { "erp_sales" } });
        var emptyIntersect = PhpPlanQ1Brine.EpcErpFilterTabsByTenantModules(
            ["nope"],
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_modules"] = new[] { "erp_sales" } });
        return new object?[]
        {
            stored,
            json,
            defaults,
            full,
            req,
            areas,
            tabs,
            filteredTabs,
            intersect,
            emptyIntersect,
            PhpPlanQ1Brine.EpcPortalErpModulesFullAccessContext() ? 1 : 0
        };
    }

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is true or 1 or 1L or 1.0 or "1";

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
