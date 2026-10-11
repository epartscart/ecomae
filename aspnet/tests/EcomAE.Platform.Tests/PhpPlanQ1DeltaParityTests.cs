using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1DeltaParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Delta");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Delta_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_epartscart_home_new_arrivals.php", PhpPlanQ1Delta.EpartscartHomeNewArrivalsPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Delta.Reset();
        SeedLive();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Delta.EpartscartHomeNewArrivalsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals(), StringComparison.Ordinal);
        Assert.Equal("epartscart", PhpPlanQ1Delta.SiteKey);
        Assert.Contains("/en/brakes/bosch-0986424407", PhpPlanQ1Delta.LastOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Eighth out", PhpPlanQ1Delta.LastOutput, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Delta.Reset();
        return name switch
        {
            "empty" => Empty(),
            "cards" => Cards(),
            "escape" => Escape(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        PhpPlanQ1Delta.HasPdo = () => false;
        PhpPlanQ1Delta.QueryImported = LiveRows;
        var none = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        PhpPlanQ1Delta.HasPdo = () => true;
        PhpPlanQ1Delta.QueryImported = () => new List<Dictionary<string, object?>>();
        var blank = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        PhpPlanQ1Delta.QueryImported = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["id"] = 3,
                ["alias"] = "skip-name",
                ["caption"] = "",
                ["title"] = "",
                ["category_url"] = "brakes",
                ["price"] = 10,
                ["file_name"] = "s.jpg"
            }
        };
        var skip = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        return new object[] { none, blank, skip };
    }

    private static object Cards()
    {
        SeedLive();
        var live = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        PhpPlanQ1Delta.ProductUrlMode = "id";
        var idMode = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        PhpPlanQ1Delta.ProductUrlMode = "alias";
        PhpPlanQ1Delta.LangHref = () => "";
        var zeroLang = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        return new object[] { live, idMode, zeroLang };
    }

    private static object Escape()
    {
        PhpPlanQ1Delta.HasPdo = () => true;
        PhpPlanQ1Delta.LangHref = () => "/ar";
        PhpPlanQ1Delta.ParseChpu = alias => alias == "a&b"
            ? new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "ac & \"me\"", ["article"] = "sku<\"1\"" }
            : PhpPlanQ1Delta.ParseAlias(alias);
        PhpPlanQ1Delta.QueryImported = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 30,
                ["alias"] = "a&b",
                ["caption"] = "TV & \"Audio\"",
                ["title"] = "TV title",
                ["category_url"] = "tv",
                ["price"] = 99.1,
                ["file_name"] = "/img/a\".jpg"
            }
        ];
        return new object[] { PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals() };
    }

    private static object Tenant()
    {
        PhpPlanQ1Delta.HasPdo = () => true;
        PhpPlanQ1Delta.LangHref = () => "/en";
        PhpPlanQ1Delta.ParseChpu = PhpPlanQ1Delta.ParseAlias;
        PhpPlanQ1Delta.QueryImported = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 40,
                ["alias"] = "acme-pad",
                ["caption"] = "Acme Pad",
                ["title"] = "Acme Pad",
                ["category_url"] = "acme-brakes",
                ["price"] = 12.5,
                ["file_name"] = "/img/acme-pad.jpg"
            }
        ];
        var acme = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        PhpPlanQ1Delta.LangHref = () => "/ar";
        PhpPlanQ1Delta.QueryImported = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 50,
                ["alias"] = "beta-rotor",
                ["caption"] = "Beta Rotor",
                ["title"] = "Beta Rotor",
                ["category_url"] = "beta-engine",
                ["price"] = 0,
                ["file_name"] = ""
            }
        ];
        var beta = PhpPlanQ1Delta.EpcEpartscartHomeNewArrivals();
        return new object[] { acme, beta };
    }

    private static void SeedLive()
    {
        PhpPlanQ1Delta.HasPdo = () => true;
        PhpPlanQ1Delta.LangHref = () => "/en";
        PhpPlanQ1Delta.ProductUrlMode = "alias";
        PhpPlanQ1Delta.ParseChpu = PhpPlanQ1Delta.ParseAlias;
        PhpPlanQ1Delta.QueryImported = LiveRows;
    }

    private static List<Dictionary<string, object?>> LiveRows()
        =>
        [
            Row(10, "bosch-0986424407", "Brake pad", "Brake pad title", "brakes", 1234.5, "a.jpg"),
            Row(11, "mann-filter", "Oil filter", "Oil filter title", "filters", 25, ""),
            Row(12, "gasket", "Gasket", "Gasket title", null, null, ""),
            Row(13, "hose-line", null, "Hose", "hoses", 8.2, "/img/hose.png"),
            Row(14, "old-one", "Old stock", "Old", "brakes", 3, ""),
            Row(15, "fifth-part", "Fifth", "Fifth title", "brakes", 4, ""),
            Row(16, "sixth-part", "Sixth", "Sixth title", "brakes", 5, ""),
            Row(17, "seventh-part", "Seventh", "Seventh title", "brakes", 6, "")
        ];

    private static Dictionary<string, object?> Row(
        int id,
        string alias,
        string? caption,
        string title,
        string? categoryUrl,
        object? price,
        string fileName)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["alias"] = alias,
            ["caption"] = caption,
            ["title"] = title,
            ["category_url"] = categoryUrl,
            ["price"] = price,
            ["file_name"] = fileName
        };

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
