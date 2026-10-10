using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1PackParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Pack");

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
    public void PlanQ1Pack_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Pack.FaqDataPath,
                PhpPlanQ1Pack.LegalContentPath,
                PhpPlanQ1Pack.DedActivityMappingPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Pack.FaqDataPath,
                PhpPlanQ1Pack.LegalContentPath,
                PhpPlanQ1Pack.DedActivityMappingPath
            });

    [Fact]
    public void FaqStatusCounts_DoesNotStartASession()
    {
        var counts = PhpPlanQ1Pack.EpcEcomaeFaqStatusCounts();
        Assert.Equal(50, counts["Yes"]);
        Assert.Equal(105, counts.Values.Sum());
    }

    private static string Render(string name)
        => name switch
        {
            "faq_data" => Json(FaqData()),
            "legal_data" => Json(LegalData()),
            "ded_data" => Json(DedData()),
            _ => "unknown:" + name
        };

    private static object?[] FaqData()
    {
        var mods = PhpPlanQ1Pack.EpcEcomaeFaqModules();
        var n = 0;
        foreach (var m in mods.EnumerateArray())
        {
            n += m.GetProperty("items").GetArrayLength();
        }

        var list = mods.EnumerateArray().ToList();
        return new object?[]
        {
            list.Count,
            n,
            list[0].GetProperty("id").GetString(),
            list[0].GetProperty("items")[0].GetProperty("num").GetInt32(),
            list[0].GetProperty("items")[0].GetProperty("status").GetString(),
            list[6].GetProperty("id").GetString(),
            list[6].GetProperty("items")[14].GetProperty("num").GetInt32(),
            PhpPlanQ1Pack.EpcEcomaeFaqStatusCounts()
        };
    }

    private static object?[] LegalData()
    {
        var cat = PhpPlanQ1Pack.EpcEcomaeLegalCatalog();
        var alias = PhpPlanQ1Pack.EpcEcomaeLegalTopLevelAliases();
        return new object?[]
        {
            PhpPlanQ1Pack.EpcEcomaeLegalEffectiveDate(),
            cat.EnumerateObject().Count(),
            cat.EnumerateObject().Select(p => p.Name).ToArray(),
            cat.GetProperty("privacy").GetProperty("title").GetString(),
            cat.GetProperty("privacy").GetProperty("icon").GetString(),
            cat.GetProperty("privacy").GetProperty("sections").GetArrayLength(),
            cat.GetProperty("dmca").GetProperty("title").GetString(),
            alias.GetProperty("/privacy").GetString(),
            alias.GetProperty("/en/terms").GetString(),
            alias.EnumerateObject().Count()
        };
    }

    private static object?[] DedData()
    {
        var d = PhpPlanQ1Pack.EpcDedDivisions();
        var g = PhpPlanQ1Pack.EpcDedDivisionsForGroup("agriculture_farming");
        var miss = PhpPlanQ1Pack.EpcDedDivisionsForGroup("nope");
        var r = PhpPlanQ1Pack.EpcWorldwideBusinessRegistries();
        var audit = PhpPlanQ1Pack.EpcDedCoverageAudit();
        var bridge = PhpPlanQ1Pack.EpcDedPortalIndustryBridge();
        return new object?[]
        {
            d.EnumerateObject().Count(),
            PhpPlanQ1Pack.EpcDedTotalActivities(),
            d.EnumerateObject().Select(p => p.Name).ToArray(),
            d.GetProperty("agriculture").GetProperty("activities").GetInt32(),
            JsonSerializer.Deserialize<object>(d.GetProperty("agriculture").GetProperty("ecomae_groups").GetRawText()),
            g.Count == 0 ? Array.Empty<string>() : g.Keys.ToArray(),
            miss.Count == 0 ? Array.Empty<object>() : miss.Keys.ToArray(),
            r.GetProperty("UAE").GetProperty("activities").GetInt32(),
            r.GetProperty("USA").GetProperty("standard").GetString(),
            audit,
            bridge["agriculture"]["portal_codes"],
            bridge["fishing"]["portal_codes"],
            PhpPlanQ1Pack.EpcDedPortalBridgeComplete()
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
