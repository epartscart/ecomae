using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpReadyNamedParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ReadyNamed");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpReadyNamed.AccessoriesTaxonomyPath,
        PhpReadyNamed.ManufacturerSynonymsPath,
        PhpReadyNamed.TenantDataPolicyPath,
        PhpReadyNamed.ConfigEditMetaPath,
        PhpReadyNamed.OrderGuidePath
    };

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
    public void ReadyNamed_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expectedResult = results[i].GetProperty("result");
            var actual = Render(name);
            if (!Same(actual, expectedResult))
            {
                failures.Add(name + " expected=" + Truncate(expectedResult.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Subset(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement
                .GetProperty("cases").EnumerateArray()
                .Select(CaseFile)
                .ToHashSet(StringComparer.Ordinal)!,
            CoveredFiles);

    [Fact]
    public void TdpHtml_UsesPinnedVersionAndUtcMonth()
    {
        var html = PhpReadyNamed.EpcTdpRenderPolicyHtml();
        Assert.Contains("Version 1.0.0", html, StringComparison.Ordinal);
        Assert.Contains("October 2026", html, StringComparison.Ordinal);
        Assert.Equal(PhpReadyNamed.TdpVersion, "1.0.0");
    }

    private static string CaseFile(JsonElement testCase)
        => testCase.GetProperty("name").GetString() switch
        {
            var name when name is not null && name.StartsWith("acc_", StringComparison.Ordinal) => PhpReadyNamed.AccessoriesTaxonomyPath,
            var name when name is not null && name.StartsWith("syn_", StringComparison.Ordinal) => PhpReadyNamed.ManufacturerSynonymsPath,
            var name when name is not null && name.StartsWith("tdp_", StringComparison.Ordinal) => PhpReadyNamed.TenantDataPolicyPath,
            var name when name is not null && name.StartsWith("cfg_", StringComparison.Ordinal) => PhpReadyNamed.ConfigEditMetaPath,
            var name when name is not null && name.StartsWith("order_", StringComparison.Ordinal) => PhpReadyNamed.OrderGuidePath,
            _ => "unknown"
        };

    private static string Render(string name)
    {
        var synonymMap = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["BOSCH"] = ["BOSCH", "BOSCH-K"]
        };
        var synonymEqMap = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["BOSCH"] = ["BOSCH", "BOSCH-K"],
            ["BOSCH-K"] = ["BOSCH", "BOSCH-K"]
        };
        var canonicalMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BOSCH-K"] = "BOSCH",
            ["BOSCH"] = "BOSCH"
        };
        var manufacturers = new List<(int Id, string Name)> { (1, "Bosch"), (2, "Febi") };
        var synonyms = new List<(int ManufacturerId, string Synonym)> { (1, "BOSCH-K"), (1, "bosch") };
        return name switch
        {
            "acc_tax" => Json(PhpReadyNamed.EpcAccTaxonomy()),
            "acc_classify_pad" => Json(PhpReadyNamed.EpcAccClassify("Ceramic brake pad")),
            "acc_classify_disc" => Json(PhpReadyNamed.EpcAccClassify("Brake disc rotor")),
            "acc_classify_none" => Json(PhpReadyNamed.EpcAccClassify("xyzzy")),
            "acc_regions" => Json(PhpReadyNamed.EpcAccWarehouseRegions()),
            "syn_norm" => Json(new[]
            {
                PhpReadyNamed.DocpartSynonymNormalizeBrand("  bosch "),
                PhpReadyNamed.DocpartSynonymNormalizeBrand("he said \"x\""),
                PhpReadyNamed.DocpartSynonymNormalizeBrand("")
            }),
            "syn_names" => Json(new[]
            {
                PhpReadyNamed.DocpartSynonymNamesForBrand("", synonymMap),
                PhpReadyNamed.DocpartSynonymNamesForBrand("bosch", synonymMap),
                PhpReadyNamed.DocpartSynonymNamesForBrand("Febi", synonymMap)
            }),
            "syn_eq" => Json(new[]
            {
                PhpReadyNamed.DocpartSynonymBrandsEquivalent("bosch", "BOSCH-K", synonymEqMap),
                PhpReadyNamed.DocpartSynonymBrandsEquivalent("bosch", "Febi", synonymEqMap),
                PhpReadyNamed.DocpartSynonymBrandsEquivalent("", "", synonymEqMap)
            }),
            "syn_canon" => Json(new[]
            {
                PhpReadyNamed.DocpartSynonymCanonicalBrand("bosch-k", canonicalMap),
                PhpReadyNamed.DocpartSynonymCanonicalBrand("Febi", canonicalMap),
                PhpReadyNamed.DocpartSynonymCanonicalBrand("", canonicalMap)
            }),
            "syn_maps" => Json(new object[]
            {
                PhpReadyNamed.DocpartLoadManufacturerSynonymMap(manufacturers, synonyms),
                PhpReadyNamed.DocpartLoadManufacturerCanonicalMap(manufacturers, synonyms)
            }),
            "tdp_sections" => Json(PhpReadyNamed.EpcTdpPolicySections()),
            "tdp_html" => Json(PhpReadyNamed.EpcTdpRenderPolicyHtml()),
            "cfg_groups" => Json(PhpReadyNamed.EpcConfigGroupMeta()),
            "cfg_group_for" => Json(new[]
            {
                PhpReadyNamed.EpcConfigGroupMetaFor(9),
                PhpReadyNamed.EpcConfigGroupMetaFor(99)
            }),
            "cfg_effect" => Json(new[]
            {
                PhpReadyNamed.EpcConfigItemFrontendEffect("shop_currency"),
                PhpReadyNamed.EpcConfigItemFrontendEffect("missing")
            }),
            "cfg_label" => Json(new[]
            {
                PhpReadyNamed.EpcConfigItemLabel("epc_contact_phone", "Phone"),
                PhpReadyNamed.EpcConfigItemLabel("unknown", "  Caption  ")
            }),
            "order_check" => Json(PhpReadyNamed.EpcOrderGuideChecklist("cp")),
            "order_snap" => Json(PhpReadyNamed.EpcOrderGuideSnapshot(
                "cp",
                "https://x.test/",
                [
                    Note(1, "new_order_to_manager", "Mgr", 1, 0, "New order"),
                    Note(2, "new_order_to_user", "User", 1, 0, "Thanks")
                ],
                [],
                new Dictionary<string, int> { ["total"] = 1, ["today"] = 0, ["last_7_days"] = 0 },
                1,
                [new Dictionary<string, object?> { ["order_id"] = 10, ["text"] = "Supplier LPO sent", ["time"] = 1700000100 }],
                [
                    new Dictionary<string, object?>
                    {
                        ["id"] = 1,
                        ["name"] = "Created",
                        ["for_created"] = 1,
                        ["for_paid"] = 0,
                        ["for_finish"] = 0,
                        ["to_manager_email"] = 1,
                        ["to_customer_email"] = 1
                    }
                ],
                [
                    new Dictionary<string, object?>
                    {
                        ["id"] = 2,
                        ["name"] = "Packed",
                        ["for_created"] = 0,
                        ["for_finish"] = 0,
                        ["to_manager_email"] = 0,
                        ["to_customer_email"] = 0
                    }
                ])),
            _ => "unknown:" + name
        };
    }

    private static Dictionary<string, object?> Note(int id, string name, string caption, int emailOn, int smsOn, string subject)
        => new()
        {
            ["id"] = id,
            ["name"] = name,
            ["caption"] = caption,
            ["email_on"] = emailOn,
            ["sms_on"] = smsOn,
            ["email_subject"] = subject
        };

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
            if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
            {
                return left.GetDouble() == right.GetDouble();
            }

            return false;
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
        => value.Length <= 220 ? value : value[..220] + "…";
}
