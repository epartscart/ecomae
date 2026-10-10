using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BightParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Bight");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Bight_MatchPhpGolden()
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
    {
        Assert.Equal("content/general_pages/epc_epartscart_spare_parts.php", PhpPlanQ1Bight.EpartscartSparePartsPath);
        Assert.Equal("content/shop/epc_spare_parts_search.php", PhpPlanQ1Bight.SparePartsSearchPath);
    }

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Bight.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Bight.EpartscartSparePartsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Bight.SparePartsSearchPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Bight.EpcEpartscartSparePartsPage(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Bight.Reset();
        return name switch
        {
            "page" => Page(),
            "inline" => Inline(),
            "ajax" => Ajax(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Page()
    {
        Seed();
        PhpPlanQ1Bight.HasPdo = () => false;
        var nodb = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        PhpPlanQ1Bight.HasPdo = () => true;
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal);
        PhpPlanQ1Bight.RequestUri = "/en/spare-parts";
        var empty = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "toyota", ["article"] = "C110J" };
        var selected = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        return new object[] { nodb, empty, selected };
    }

    private static object Inline()
    {
        Seed();
        PhpPlanQ1Bight.HasPdo = () => true;
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota", ["article"] = "1310154101" };
        var hit = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal);
        PhpPlanQ1Bight.RequestUri = "/en/spare-parts/Toyota/NO-SUCH";
        var miss = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota" };
        PhpPlanQ1Bight.RequestUri = "/en/spare-parts/Ignored/1310154101";
        var getWins = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        return new object[] { hit, miss, getWins };
    }

    private static object Ajax()
    {
        Seed();
        PhpPlanQ1Bight.AjaxPdoOk = () => true;
        PhpPlanQ1Bight.Request = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota", ["article"] = "A" };
        var shortArt = PhpPlanQ1Bight.EpcSparePartsSearchAjax();
        PhpPlanQ1Bight.Request = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota", ["article"] = "1310154101" };
        var hit = PhpPlanQ1Bight.EpcSparePartsSearchAjax();
        PhpPlanQ1Bight.Request = new Dictionary<string, string>(StringComparer.Ordinal) { ["manufacturer"] = "Toyota", ["article"] = "NO-SUCH" };
        var alias = PhpPlanQ1Bight.EpcSparePartsSearchAjax();
        PhpPlanQ1Bight.AjaxPdoOk = () => false;
        PhpPlanQ1Bight.Request = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota", ["article"] = "1310154101" };
        var fail = PhpPlanQ1Bight.EpcSparePartsSearchAjax();
        return new object[] { shortArt, hit, alias, fail };
    }

    private static object Tenant()
    {
        Seed();
        PhpPlanQ1Bight.HasPdo = () => true;
        PhpPlanQ1Bight.AjaxPdoOk = () => true;
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota", ["article"] = "1310154101" };
        var acme = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        PhpPlanQ1Bight.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Beta", ["article"] = "1310154101" };
        var beta = PhpPlanQ1Bight.EpcEpartscartSparePartsPage();
        PhpPlanQ1Bight.Request = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Toyota", ["article"] = "1310154101" };
        var acmeAjax = PhpPlanQ1Bight.EpcSparePartsSearchAjax();
        PhpPlanQ1Bight.Request = new Dictionary<string, string>(StringComparer.Ordinal) { ["brand"] = "Beta", ["article"] = "1310154101" };
        var betaAjax = PhpPlanQ1Bight.EpcSparePartsSearchAjax();
        return new object[] { acme, beta, acmeAjax, betaAjax };
    }

    private static void Seed()
    {
        PhpPlanQ1Bight.LangPrefix = () => "/en";
        PhpPlanQ1Bight.Brands = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "Toyota", ["label"] = "Toyota" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "Bosch", ["label"] = "Bosch" }
        ];
        PhpPlanQ1Bight.Search = (brand, article) => (brand + "|" + article) switch
        {
            "Toyota|1310154101" => Hit("Acme City", 3, 12.5, 18.5, 7, "/en/oil-filters/toyota/1310154101", "/en/parts/Toyota/1310154101"),
            "Toyota|NO-SUCH" => Miss(),
            "Beta|1310154101" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["brand"] = "Toyota",
                ["article"] = "1310154101",
                ["in_warehouse"] = true,
                ["qty"] = 1,
                ["sell_price"] = 9,
                ["currency"] = "AED",
                ["warehouse_rows"] = new List<Dictionary<string, object?>> { new(StringComparer.Ordinal) { ["warehouse"] = "Beta Town" } },
                ["product_url"] = "",
                ["parts_url"] = "",
                ["redirect_url"] = "",
                ["message"] = ""
            },
            _ => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Enter a valid part number (at least 2 characters)."
            }
        };
    }

    private static Dictionary<string, object?> Hit(string warehouse, double qty, double cost, double sell, int productId, string productUrl, string partsUrl)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["brand"] = "Toyota",
            ["article"] = "1310154101",
            ["in_warehouse"] = true,
            ["qty"] = qty,
            ["warehouse_cost"] = cost,
            ["sell_price"] = sell,
            ["currency"] = "AED",
            ["warehouse_rows"] = new List<Dictionary<string, object?>> { new(StringComparer.Ordinal) { ["warehouse"] = warehouse } },
            ["product_id"] = productId,
            ["product_url"] = productUrl,
            ["parts_url"] = partsUrl,
            ["redirect_url"] = "",
            ["message"] = ""
        };

    private static Dictionary<string, object?> Miss()
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["brand"] = "Toyota",
            ["article"] = "NO-SUCH",
            ["in_warehouse"] = false,
            ["qty"] = 0,
            ["sell_price"] = 0,
            ["currency"] = "AED",
            ["warehouse_rows"] = new List<Dictionary<string, object?>>(),
            ["product_url"] = "",
            ["parts_url"] = "/en/parts/Toyota/NO-SUCH",
            ["redirect_url"] = "",
            ["message"] = "Not in stock — contact us for availability."
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
