using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1FirthParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Firth");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Firth_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_electronicae_home_product_lines.php", PhpPlanQ1Firth.ElectronicaeHomeProductLinesPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Firth.Reset();
        SeedTiles();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Firth.ElectronicaeHomeProductLinesPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Firth.EpcElectronicaeHomeProductLines(), StringComparison.Ordinal);
        Assert.Contains("/en/phones", PhpPlanQ1Firth.EpcElPlHref("/en", "/phones"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Firth.Reset();
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
        PhpPlanQ1Firth.HasPdo = () => false;
        var none = PhpPlanQ1Firth.EpcElectronicaeHomeProductLines();
        PhpPlanQ1Firth.HasPdo = () => true;
        PhpPlanQ1Firth.SiteKey = () => "electronicae";
        PhpPlanQ1Firth.ProductLineTiles = _ => new List<Dictionary<string, object?>>();
        PhpPlanQ1Firth.AllLinesHref = _ => "/catalogue/lines";
        var blank = PhpPlanQ1Firth.EpcElectronicaeHomeProductLines();
        return new object[] { none, blank };
    }

    private static object Tiles()
    {
        SeedTiles();
        var html = PhpPlanQ1Firth.EpcElectronicaeHomeProductLines();
        PhpPlanQ1Firth.LangHref = () => "";
        PhpPlanQ1Firth.AllLinesHref = _ => "/all";
        var zeroLang = PhpPlanQ1Firth.EpcElectronicaeHomeProductLines();
        return new object[] { html, zeroLang };
    }

    private static object Escape()
    {
        PhpPlanQ1Firth.HasPdo = () => true;
        PhpPlanQ1Firth.LangHref = () => "/ar";
        PhpPlanQ1Firth.SiteKey = () => "electronicae";
        PhpPlanQ1Firth.AllLinesHref = _ => "/lines?x=1&y=2";
        PhpPlanQ1Firth.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "TV & \"Audio\"",
                ["href"] = "/tv?q=a&b=1",
                ["image"] = "/img/a\".jpg",
                ["icon"] = "fa-tv",
                ["accent"] = "red;alert(1)",
                ["product_count"] = 3,
                ["trend"] = "hot"
            }
        };
        return new object[] { PhpPlanQ1Firth.EpcElectronicaeHomeProductLines() };
    }

    private static object Tenant()
    {
        PhpPlanQ1Firth.HasPdo = () => true;
        PhpPlanQ1Firth.LangHref = () => "/en";
        PhpPlanQ1Firth.SiteKey = () => "acme";
        PhpPlanQ1Firth.AllLinesHref = _ => "/acme/lines";
        PhpPlanQ1Firth.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "Acme Phones",
                ["href"] = "/acme-phones",
                ["image"] = "/img/acme-phones.jpg",
                ["icon"] = "fa-mobile",
                ["accent"] = "#e10a0a",
                ["product_count"] = 4,
                ["trend"] = "hot"
            }
        };
        var acme = PhpPlanQ1Firth.EpcElectronicaeHomeProductLines();
        PhpPlanQ1Firth.LangHref = () => "/ar";
        PhpPlanQ1Firth.SiteKey = () => "beta";
        PhpPlanQ1Firth.AllLinesHref = _ => "/beta/lines";
        PhpPlanQ1Firth.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "Beta Laptops",
                ["href"] = "/beta-laptops",
                ["image"] = "",
                ["icon"] = "fa-laptop",
                ["accent"] = "#202124",
                ["product_count"] = 0,
                ["trend"] = ""
            }
        };
        var beta = PhpPlanQ1Firth.EpcElectronicaeHomeProductLines();
        return new object[] { acme, beta };
    }

    private static void SeedTiles()
    {
        PhpPlanQ1Firth.HasPdo = () => true;
        PhpPlanQ1Firth.LangHref = () => "/en";
        PhpPlanQ1Firth.SiteKey = () => "electronicae";
        PhpPlanQ1Firth.AllLinesHref = _ => "/catalogue/lines";
        PhpPlanQ1Firth.ProductLineTiles = _ => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal)
            {
                ["name"] = "Phones",
                ["href"] = "/phones",
                ["image"] = "/img/phones.jpg",
                ["icon"] = "fa-mobile",
                ["accent"] = "#1a73e8",
                ["product_count"] = 2,
                ["trend"] = "hot"
            },
            new(StringComparer.Ordinal)
            {
                ["name"] = "Laptops",
                ["href"] = "/laptops",
                ["image"] = "",
                ["icon"] = "fa-laptop",
                ["accent"] = "#5f6368",
                ["product_count"] = 1,
                ["trend"] = ""
            },
            new(StringComparer.Ordinal)
            {
                ["name"] = "Soon",
                ["href"] = "/soon",
                ["product_count"] = 0
            }
        };
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
