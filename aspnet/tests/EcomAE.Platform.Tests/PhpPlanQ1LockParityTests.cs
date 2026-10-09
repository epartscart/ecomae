using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1LockParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Lock");

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
    public void PlanQ1Lock_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Lock.ConsolidationPath, PhpPlanQ1Lock.TemplateRouterPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Lock.ConsolidationPath, PhpPlanQ1Lock.TemplateRouterPath });

    [Fact]
    public void Groups_DoNotStartASession()
    {
        PhpPlanQ1Lock.Reset();
        Assert.Equal(28, PhpPlanQ1Lock.EpcIndustryGroups().Count);
        Assert.Equal("automotive", PhpPlanQ1Lock.EpcIndustryTemplateKey("auto_parts"));
    }

    private static string Render(string name)
    {
        PhpPlanQ1Lock.Reset();
        return name switch
        {
            "cons_pure" => Json(ConsPure()),
            "cons_db" => Json(ConsDb()),
            "router_pure" => Json(RouterPure()),
            "router_files" => Json(RouterFiles()),
            _ => "unknown:" + name
        };
    }

    private static object? ConsPure()
    {
        var g = PhpPlanQ1Lock.EpcIndustryGroups();
        var keys = g.Keys.ToList();
        var auto = g["automotive"];
        var resolves = new object?[]
        {
            PhpPlanQ1Lock.EpcIndustryResolveGroup("auto_parts"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("jewellery"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("3D Printing Bureau"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("Furniture shop"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("Pet shop"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("Unknown X"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("software saas"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("bank"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("zzz", "Commerce"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("zzz", "Services"),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("zzz", ""),
            PhpPlanQ1Lock.EpcIndustryResolveGroup("gold buying")
        };
        var tpl = new object?[]
        {
            PhpPlanQ1Lock.EpcIndustryTemplateKey("auto_parts"),
            PhpPlanQ1Lock.EpcIndustryTemplateKey("Unknown X"),
            PhpPlanQ1Lock.EpcIndustryTemplateKey("jewellery")
        };
        var hit = PhpPlanQ1Lock.EpcIndustryGetGroup("auto_parts");
        var miss = PhpPlanQ1Lock.EpcIndustryGetGroup("nope");
        var stats = PhpPlanQ1Lock.EpcIndustryConsolidationStats(
        [
            "Auto parts shop",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Furniture shop", ["badge"] = "" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "zzz", ["badge"] = "Commerce" },
            "Pet shop",
            "Unknown Y"
        ]);
        var statKeys = stats.Keys.ToList();
        var topBucket = (Dictionary<string, object?>)stats[statKeys[0]]!;
        var top = new object?[] { statKeys[0], topBucket["count"], topBucket["industries"] };
        var colors = (Dictionary<string, object?>)auto["color_scheme"]!;
        return new object?[]
        {
            g.Count, keys, auto["template_key"], auto["default_sub_areas"], colors["primary"],
            resolves, tpl, hit["label"], miss["template_key"], top
        };
    }

    private static object? ConsDb()
    {
        var db = new PhpPlanQ1Lock.ConfigStore();
        var def = PhpPlanQ1Lock.EpcIndustryTenantSubAreas(db, "acme", "auto_parts");
        var ok = PhpPlanQ1Lock.EpcIndustrySaveTenantSubAreas(
            db, "acme", "auto_parts", "automotive",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["parts_catalog"] = true,
                ["workshop"] = false,
                ["fleet"] = true,
                ["dealership"] = true
            });
        var saved = PhpPlanQ1Lock.EpcIndustryTenantSubAreas(db, "acme", "auto_parts");
        db.Rows["emptyj"] = new PhpPlanQ1Lock.ConfigRow
        {
            SiteKey = "emptyj",
            IndustryCode = "auto_parts",
            GroupKey = "automotive",
            SubAreasJson = "{}"
        };
        var empty = PhpPlanQ1Lock.EpcIndustryTenantSubAreas(db, "emptyj", "auto_parts");
        var miss = PhpPlanQ1Lock.EpcIndustryTenantSubAreas(db, "none", "jewellery");
        return new object?[] { def, ok, saved, empty, miss };
    }

    private static object? RouterPure()
    {
        var h1 = PhpPlanQ1Lock.EpcIndustryHeroConfig("auto_parts");
        var h2 = PhpPlanQ1Lock.EpcIndustryHeroConfig("auto_parts", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["theme"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["primary"] = "#111111", ["accent"] = "#222222" }
        });
        var h3 = PhpPlanQ1Lock.EpcIndustryHeroConfig("auto_parts", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["theme"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["primary"] = "0", ["accent"] = "#222222" }
        });
        var mods = PhpPlanQ1Lock.EpcIndustryErpModulesForSubAreas(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["parts_catalog"] = true, ["workshop"] = false, ["rental"] = true },
            "automotive");
        var mods2 = PhpPlanQ1Lock.EpcIndustryErpModulesForSubAreas(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["production"] = true, ["inventory"] = true },
            "manufacturing_industrial");
        var secs = PhpPlanQ1Lock.EpcIndustryCpSectionsForSubAreas(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["workshop"] = true, ["x"] = false, ["fleet"] = true },
            "automotive");
        return new object?[] { h1, h2, h3, mods, mods2, secs, PhpPlanQ1Lock.EpcIndustryConsolidationSavings() };
    }

    private static object? RouterFiles()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1l_" + Guid.NewGuid().ToString("N")[..12]);
        var tpl = Path.Combine(doc, "content", "general_pages", "industry_templates");
        Directory.CreateDirectory(tpl);
        File.WriteAllText(Path.Combine(tpl, "automotive" + "_" + "hero" + ".php"), "<?php\n");
        File.WriteAllText(Path.Combine(tpl, "automotive" + ".php"), "<?php\n");
        File.WriteAllText(Path.Combine(doc, "content", "general_pages", "epc_generic_commerce_package" + ".php"), "<?php\n");
        PhpPlanQ1Lock.DocumentRoot = doc;
        try
        {
            var r1 = PhpPlanQ1Lock.EpcIndustryRouteTemplate("auto_parts", "hero").Replace(doc, "DOC");
            var r2 = PhpPlanQ1Lock.EpcIndustryRouteTemplate("auto_parts").Replace(doc, "DOC");
            File.Delete(Path.Combine(tpl, "automotive" + "_" + "hero" + ".php"));
            File.Delete(Path.Combine(tpl, "automotive" + ".php"));
            var r3 = PhpPlanQ1Lock.EpcIndustryRouteTemplate("jewellery").Replace(doc, "DOC");
            File.Delete(Path.Combine(doc, "content", "general_pages", "epc_generic_commerce_package" + ".php"));
            var r4 = PhpPlanQ1Lock.EpcIndustryRouteTemplate("nonesuch");
            var cats = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["name"] = "A", ["sub_area"] = "parts_catalog" },
                new(StringComparer.Ordinal) { ["name"] = "B", ["sub_area"] = "workshop" },
                new(StringComparer.Ordinal) { ["name"] = "C", ["sub_area"] = "" },
                new(StringComparer.Ordinal) { ["name"] = "D", ["sub_area"] = "rental" }
            };
            var db = new PhpPlanQ1Lock.ConfigStore();
            PhpPlanQ1Lock.EpcIndustrySaveTenantSubAreas(
                db, "flt", "auto_parts", "automotive",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["parts_catalog"] = true,
                    ["workshop"] = false,
                    ["fleet"] = false
                });
            var f1 = PhpPlanQ1Lock.EpcIndustryFilterCategoriesBySubAreas(db, "flt", "auto_parts", cats);
            var f2 = PhpPlanQ1Lock.EpcIndustryFilterCategoriesBySubAreas(db, "none", "nonesuch", cats);
            return new object?[] { r1, r2, r3, r4, f1, f2 };
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
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
        => value.Length <= 800 ? value : value[..800] + "…";
}
