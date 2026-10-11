using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1CreekParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Creek");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Creek_MatchPhpGolden()
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
                var exp = expected.GetRawText();
                var got = Json(actual);
                failures.Add(name + " exp=" + exp[..Math.Min(280, exp.Length)] + " got=" + got[..Math.Min(280, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/general_pages/epc_epartscart_home_product_lines.php", PhpPlanQ1Creek.EpartscartHomeProductLinesPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Creek.Reset();
        SeedLive();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Creek.EpartscartHomeProductLinesPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Creek.EpcEpartscartHomeProductLines(), StringComparison.Ordinal);
        Assert.Equal("epc_pl_tiles:v1:epartscart:8", PhpPlanQ1Creek.LastCacheKey);
        Assert.Contains("/en/brakes", PhpPlanQ1Creek.EpcEpPlHref("/en", "/brakes"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Creek.Reset();
        return name switch
        {
            "empty" => Empty(),
            "tiles" => Tiles(),
            "escape" => Escape(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        PhpPlanQ1Creek.HasPdo = () => false;
        PhpPlanQ1Creek.ProductLineTiles = _ => Sample();
        var none = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        PhpPlanQ1Creek.HasPdo = () => true;
        PhpPlanQ1Creek.ProductLineTiles = _ => new List<Dictionary<string, object?>>();
        var blank = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        PhpPlanQ1Creek.HomePerfFast = () => 1;
        PhpPlanQ1Creek.PerfCacheGet = _ => null;
        PhpPlanQ1Creek.ProductLineTiles = _ => Sample();
        var fast = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        return new object[] { none, blank, fast };
    }

    private static object Tiles()
    {
        SeedLive();
        var live = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        PhpPlanQ1Creek.PerfCacheGet = _ => Sample();
        PhpPlanQ1Creek.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["name"] = "Ignored" }
        };
        PhpPlanQ1Creek.AllLinesHref = _ => "/all";
        var cached = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        PhpPlanQ1Creek.LangHref = () => "";
        PhpPlanQ1Creek.HomePerfFast = () => "0";
        PhpPlanQ1Creek.PerfCacheGet = _ => null;
        PhpPlanQ1Creek.ProductLineTiles = _ => Sample();
        PhpPlanQ1Creek.AllLinesHref = _ => "/catalogue/lines";
        var zeroLang = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        return new object[] { live, cached, zeroLang };
    }

    private static object Escape()
    {
        PhpPlanQ1Creek.HasPdo = () => true;
        PhpPlanQ1Creek.LangHref = () => "/ar";
        PhpPlanQ1Creek.AllLinesHref = _ => "/lines?x=1&y=2";
        PhpPlanQ1Creek.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "TV & \"Audio\"",
                ["href"] = "/tv?q=a&b=1",
                ["image"] = "/img/a\".jpg",
                ["icon"] = "fa-tv",
                ["product_count"] = 3
            }
        };
        return new object[] { PhpPlanQ1Creek.EpcEpartscartHomeProductLines() };
    }

    private static object Tenant()
    {
        PhpPlanQ1Creek.HasPdo = () => true;
        PhpPlanQ1Creek.LangHref = () => "/en";
        PhpPlanQ1Creek.AllLinesHref = _ => "/acme/lines";
        PhpPlanQ1Creek.PerfCacheGet = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "Acme Brakes",
                ["href"] = "/acme-brakes",
                ["image"] = "/img/acme-brakes.jpg",
                ["icon"] = "fa-car",
                ["product_count"] = 4
            }
        };
        var acme = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        PhpPlanQ1Creek.LangHref = () => "/ar";
        PhpPlanQ1Creek.AllLinesHref = _ => "/beta/lines";
        PhpPlanQ1Creek.PerfCacheGet = _ => null;
        PhpPlanQ1Creek.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "Beta Engine",
                ["href"] = "/beta-engine",
                ["image"] = "",
                ["icon"] = "fa-cogs",
                ["product_count"] = 0
            }
        };
        var beta = PhpPlanQ1Creek.EpcEpartscartHomeProductLines();
        return new object[] { acme, beta };
    }

    private static void SeedLive()
    {
        PhpPlanQ1Creek.HasPdo = () => true;
        PhpPlanQ1Creek.LangHref = () => "/en";
        PhpPlanQ1Creek.AllLinesHref = _ => "/catalogue/lines";
        PhpPlanQ1Creek.PerfCacheGet = _ => null;
        PhpPlanQ1Creek.ProductLineTiles = _ => Sample();
    }

    private static List<Dictionary<string, object?>> Sample()
        =>
        [
            new(StringComparer.Ordinal)
            {
                ["name"] = "Brakes",
                ["href"] = "/brakes",
                ["image"] = "/img/brakes.jpg",
                ["icon"] = "fa-car",
                ["product_count"] = 2
            },
            new(StringComparer.Ordinal)
            {
                ["name"] = "Engine",
                ["href"] = "/engine",
                ["image"] = "",
                ["icon"] = "fa-cogs",
                ["product_count"] = 1
            },
            new(StringComparer.Ordinal)
            {
                ["name"] = "Soon",
                ["href"] = "/soon",
                ["product_count"] = 0
            }
        ];

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
