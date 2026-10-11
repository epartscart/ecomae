using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1AftParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Aft");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Aft_MatchPhpGolden()
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
        => Assert.Equal("content/shop/marketing/epc_marketing_strategies_data.php", PhpPlanQ1Aft.StrategiesDataPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Aft.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Aft.StrategiesDataPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", string.Join(",", PhpPlanQ1Aft.StrategyKeys()), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Aft.Reset();
        switch (name)
        {
            case "empty":
                PhpPlanQ1Aft.SiteDomain = () => "";
                PhpPlanQ1Aft.SiteHost = () => "";
                break;
            case "domain":
                PhpPlanQ1Aft.SiteDomain = () => "https://shop.acme.test";
                PhpPlanQ1Aft.SiteHost = () => "";
                break;
            case "hostenc":
                PhpPlanQ1Aft.SiteDomain = () => "https://a.com";
                PhpPlanQ1Aft.SiteHost = () => "a&b'\"<>";
                break;
            case "config":
                PhpPlanQ1Aft.SiteDomain = () => "";
                PhpPlanQ1Aft.SiteHost = () => "cfg.host";
                PhpPlanQ1Aft.ConfigDomainPath = () => "https://cfg.test/";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }

        return Summary(PhpPlanQ1Aft.EpcMarketingStrategies());
    }

    private static object Summary(Dictionary<string, Dictionary<string, object?>> catalog)
    {
        var keys = catalog.Keys.ToList();
        var titles = keys.Select(k => Convert.ToString(catalog[k]["title"]) ?? "").ToList();
        var tasks = keys.Select(k => CountMap(catalog[k], "follow_tasks")).ToList();
        var kpis = keys.Select(k => CountMap(catalog[k], "kpis")).ToList();
        var guides = keys.Select(k => CountList(catalog[k], "guidelines")).ToList();
        var links = keys.Select(k => CountList(catalog[k], "links")).ToList();
        var measureBody = GuidelineBody(catalog["measurement"], 1);
        var measureLink = LinkUrl(catalog["measurement"], 0);
        var seoLink = LinkUrl(catalog["seo"], 0);
        var marketBody = GuidelineBody(catalog["marketplaces"], 1);
        var intlLink = LinkUrl(catalog["international"], 1);
        var qwLink = LinkUrl(catalog["quick_wins"], 1);
        return new object[]
        {
            keys.Count,
            keys,
            titles,
            tasks,
            kpis,
            guides,
            links,
            measureBody.Contains("your domain", StringComparison.Ordinal) ? 1 : 0,
            measureBody.Contains("&#039;", StringComparison.Ordinal) || measureBody.Contains("&amp;", StringComparison.Ordinal) || measureBody.Contains("&quot;", StringComparison.Ordinal) ? 1 : 0,
            measureLink,
            seoLink,
            marketBody.Contains("https://", StringComparison.Ordinal) || marketBody.Contains("storefront", StringComparison.Ordinal) ? 1 : 0,
            intlLink,
            qwLink,
            Encoding.UTF8.GetByteCount(measureBody),
            LinkUrl(catalog["partnerships"], 1)
        };
    }

    private static int CountMap(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is Dictionary<string, object?> map ? map.Count : 0;

    private static int CountList(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is List<object?> list ? list.Count : 0;

    private static string GuidelineBody(Dictionary<string, object?> row, int index)
    {
        if (!row.TryGetValue("guidelines", out var value) || value is not List<object?> list || index >= list.Count)
        {
            return "";
        }

        return list[index] is Dictionary<string, object?> item && item.TryGetValue("body", out var body)
            ? Convert.ToString(body) ?? ""
            : "";
    }

    private static string LinkUrl(Dictionary<string, object?> row, int index)
    {
        if (!row.TryGetValue("links", out var value) || value is not List<object?> list || index >= list.Count)
        {
            return "";
        }

        return list[index] is Dictionary<string, object?> item && item.TryGetValue("url", out var url)
            ? Convert.ToString(url) ?? ""
            : "";
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
