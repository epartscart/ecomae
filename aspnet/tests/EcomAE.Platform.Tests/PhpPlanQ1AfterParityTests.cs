using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1AfterParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1After");

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
    public void PlanQ1After_MatchPhpGolden()
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
        => Assert.Equal(PhpPlanQ1After.IndustryLiveBridgePath, CaseFile("live_keys"));

    [Fact]
    public void LiveUrl_DoesNotStartASession()
        => Assert.Equal("https://automotive.ecomae.com/", PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("auto_parts"));

    private static string CaseFile(string name)
        => name.StartsWith("live_", StringComparison.Ordinal) ? PhpPlanQ1After.IndustryLiveBridgePath : "unknown";

    private static string Render(string name)
    {
        var defs = PhpPlanQ1After.EpcPortalIndustryLiveDefs();
        return name switch
        {
            "live_keys" => Json(new object[]
            {
                defs.EnumerateObject().Count(),
                defs.EnumerateObject().Select(p => p.Name).ToArray(),
                JsonSerializer.Deserialize<object>(defs.GetProperty("auto_parts").GetRawText())!,
                JsonSerializer.Deserialize<object>(defs.GetProperty("tax_advisory").GetRawText())!,
                defs.GetProperty("nutrition_supplements").GetProperty("mode").GetString()!,
                defs.GetProperty("nutrition_supplements").GetProperty("sub_label").GetString()!,
                JsonSerializer.Deserialize<object>(defs.GetProperty("nutrition_supplements").GetProperty("categories").GetRawText())!
            }),
            "live_url" => Json(new[]
            {
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("auto_parts"),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("TAX_ADVISORY!"),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("nutrition_supplements"),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("food_beverage"),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("it_hardware_accessories"),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("nope"),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl(""),
                PhpPlanQ1After.EpcPortalIndustryLiveStorefrontUrl("printing_signage")
            }),
            "live_merge" => Json(LiveMerge()),
            "live_audit" => Json(LiveAudit()),
            _ => "unknown:" + name
        };
    }

    private static object?[] LiveMerge()
    {
        var empty = PhpPlanQ1After.EpcPortalMergeLiveSubsIntoIndustry(new Dictionary<string, object?> { ["hero_photo"] = "HERO" }, "healthcare");
        var exist = PhpPlanQ1After.EpcPortalMergeLiveSubsIntoIndustry(
            new Dictionary<string, object?>
            {
                ["sub_industries"] = new[] { "Food supplements & nutrition", "Clinics" },
                ["sub_industry_products"] = new Dictionary<string, object?>
                {
                    ["Food supplements & nutrition"] = new Dictionary<string, object?>
                    {
                        ["photo"] = "KEEP",
                        ["desc"] = "old",
                        ["categories"] = new[] { "X" },
                        ["products"] = Array.Empty<object>()
                    }
                }
            },
            "healthcare");
        var auto = PhpPlanQ1After.EpcPortalMergeLiveSubsIntoIndustry(
            new Dictionary<string, object?> { ["sub_industries"] = new[] { "Cars" } },
            "automotive");
        var bad = PhpPlanQ1After.EpcPortalMergeLiveSubsIntoIndustry(
            new Dictionary<string, object?> { ["sub_industries"] = new[] { "Z" } },
            "Nope!");
        var emptyPacks = (Dictionary<string, Dictionary<string, object?>>)empty["sub_industry_products"]!;
        var existPacks = (Dictionary<string, Dictionary<string, object?>>)exist["sub_industry_products"]!;
        var keep = existPacks["Food supplements & nutrition"];
        return new object?[]
        {
            empty["sub_industries"],
            emptyPacks["Food supplements & nutrition"]["photo"],
            emptyPacks["Food supplements & nutrition"]["categories"],
            exist["sub_industries"],
            keep["photo"],
            auto,
            bad
        };
    }

    private static Dictionary<string, object?> LiveAudit()
    {
        var industries = new Dictionary<string, object?>
        {
            ["auto_parts"] = new Dictionary<string, object?>(),
            ["tax_advisory"] = new Dictionary<string, object?>(),
            ["medical"] = new Dictionary<string, object?>(),
            ["nutrition_supplements"] = new Dictionary<string, object?>(),
            ["unknown_x"] = new Dictionary<string, object?>(),
            ["printing_signage"] = new Dictionary<string, object?>(),
            ["platform_host"] = new Dictionary<string, object?>(),
            ["erp_standalone"] = new Dictionary<string, object?>()
        };
        var templates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["automotive"] = "<?php return array('Vehicle dealership & sales' => array());\n",
            ["professional"] = "<?php $x = \"Tax advisory & compliance\";\n",
            ["healthcare"] = "<?php // no medical label\n"
        };
        return PhpPlanQ1After.EpcPortalIndustryLiveAudit(industries, templates);
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
        => value.Length <= 220 ? value : value[..220] + "…";
}
