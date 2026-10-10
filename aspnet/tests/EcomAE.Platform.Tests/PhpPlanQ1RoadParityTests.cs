using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1RoadParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Road");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Road_MatchPhpGolden()
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
                failures.Add(name + " exp=" + exp[..Math.Min(400, exp.Length)] + " got=" + got[..Math.Min(400, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/shop/epc_spare_parts_warehouse.php", PhpPlanQ1Road.SparePartsWarehousePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Road.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Road.SparePartsWarehousePath, StringComparison.Ordinal);
        var miss = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "NO-SUCH", Cfg());
        Assert.DoesNotContain("Set-Cookie", Json(miss), StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", Json(miss), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Road.Reset();
        return name switch
        {
            "brands" => Brands(),
            "urls" => Urls(),
            "search" => Search(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Brands()
    {
        PhpPlanQ1Road.QueryBrands = () => [];
        var empty = PhpPlanQ1Road.EpcSparePartsOemBrands();
        PhpPlanQ1Road.Reset();
        PhpPlanQ1Road.TaxSeedTree = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["slug"] = "other",
                ["children"] = new List<Dictionary<string, object?>> { new(StringComparer.Ordinal) { ["name"] = "SkipMe" } }
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["slug"] = "auto-oem-brands",
                ["children"] = new List<Dictionary<string, object?>>
                {
                    new(StringComparer.Ordinal) { ["name"] = "Mahindra & Mahindra" },
                    new(StringComparer.Ordinal) { ["name"] = "" },
                    new(StringComparer.Ordinal) { ["name"] = "Toyota" }
                }
            }
        ];
        PhpPlanQ1Road.QueryBrands = () => ["MANN FILTER", "toyota", "Acme Oils", ""];
        var fresh = PhpPlanQ1Road.EpcSparePartsOemBrands();
        var labels = fresh.Select(row => Convert.ToString(row["label"])).ToList();
        return new object[] { empty, fresh, labels.Contains("MANN FILTER") ? 1 : 0, labels.Contains("Mahindra") ? 1 : 0, labels.Contains("SkipMe") ? 1 : 0 };
    }

    private static object Urls()
    {
        PhpPlanQ1Road.LangPrefix = () => "/en";
        PhpPlanQ1Road.QueryProduct = id => id switch
        {
            7 => Product(7, "toyota/1310154101", "oil-filters", 1),
            8 => Product(8, "", "oil-filters", 1),
            10 => Product(10, "orphan", "", 1),
            _ => null
        };
        var sold = true;
        PhpPlanQ1Road.QueryMinPrice = id =>
        {
            if (!sold)
            {
                throw new InvalidOperationException("missing");
            }

            return id == 7 ? 12.25 : 0;
        };
        var cfgAlias = Cfg("alias");
        var cfgId = Cfg("id");
        var zero = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(0, cfgAlias);
        var missing = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(99, cfgAlias);
        var hidden = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(9, cfgAlias);
        var alias = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(7, cfgAlias);
        var idMode = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(7, cfgId);
        var noAlias = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(8, cfgAlias);
        var orphan = PhpPlanQ1Road.EpcSparePartsCatalogueProductUrl(10, cfgAlias);
        var sellZero = PhpPlanQ1Road.EpcSparePartsCatalogueSellPrice(0);
        var sellMin = PhpPlanQ1Road.EpcSparePartsCatalogueSellPrice(7);
        var sellNone = PhpPlanQ1Road.EpcSparePartsCatalogueSellPrice(9);
        sold = false;
        var sellMissing = PhpPlanQ1Road.EpcSparePartsCatalogueSellPrice(7);
        return new object[] { zero, missing, hidden, alias, idMode, noAlias, orphan, sellZero, sellMin, sellNone, sellMissing };
    }

    private static object Search()
    {
        SeedSearch();
        var cfg = Cfg();
        var shortArt = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "A", cfg);
        var punct = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "A-", cfg);
        var noBrand = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("", "13101-54101", cfg);
        var miss = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "NO-SUCH", cfg);
        var hit = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "13101-54101", cfg);
        var vw = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("VW", "06D115562", cfg);
        var wrongBrand = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Honda", "13101-54101", cfg);
        PhpPlanQ1Road.QueryFailed = () => true;
        var failed = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "13101-54101", cfg);
        return new object[] { shortArt, punct, noBrand, miss, hit, vw, wrongBrand, failed };
    }

    private static object Tenant()
    {
        PhpPlanQ1Road.LangPrefix = () => "/en";
        PhpPlanQ1Road.DemandPartUrl = (_, brand, article) => "/en/parts/" + brand + "/" + article;
        PhpPlanQ1Road.QueryPriceRows = _ =>
        [
            Price("Toyota", "1310154101", "13101-54101", 12.5, 3, "Acme City", "Acme list", 4)
        ];
        var acme = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "1310154101", Cfg());
        PhpPlanQ1Road.QueryPriceRows = _ =>
        [
            Price("Toyota", "1310154101", "13101-54101", 9, 1, "Beta Town", "Beta list", 5)
        ];
        var beta = PhpPlanQ1Road.EpcSparePartsWarehouseSearch("Toyota", "1310154101", Cfg());
        return new object[] { acme, beta };
    }

    private static void SeedSearch()
    {
        PhpPlanQ1Road.LangPrefix = () => "/en";
        PhpPlanQ1Road.FindCatalogue = key => key == "toyota:1310154101" ? 7 : 0;
        PhpPlanQ1Road.QueryProduct = id => id == 7 ? Product(7, "toyota/1310154101", "oil-filters", 1) : null;
        PhpPlanQ1Road.QueryMinPrice = id => id == 7 ? 18.5 : 0;
        PhpPlanQ1Road.DemandPartUrl = (_, brand, article) => "/en/parts/" + brand + "/" + article;
        PhpPlanQ1Road.QueryPriceRows = articleNorm => articleNorm switch
        {
            "1310154101" => [Price("Toyota", "13101-54101", "13101 54101", 12.5, 3, "Acme City", "Acme list", 4)],
            "06D115562" => [Price("Volkswagen", "06D115562", "06D 115 562", 22, 0, "Acme City", "Acme list", 4)],
            _ => []
        };
    }

    private static Dictionary<string, object?> Cfg(string mode = "alias")
        => new(StringComparer.Ordinal) { ["product_url"] = mode, ["multilang"] = 1 };

    private static Dictionary<string, object?> Product(int id, string alias, string categoryUrl, int published)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["alias"] = alias,
            ["caption"] = "Filter",
            ["category_url"] = categoryUrl,
            ["published_flag"] = published
        };

    private static Dictionary<string, object?> Price(string brand, string article, string show, double price, double exist, string storage, string list, int priceId)
        => new(StringComparer.Ordinal)
        {
            ["manufacturer"] = brand,
            ["article"] = article,
            ["article_show"] = show,
            ["price"] = price,
            ["exist"] = exist,
            ["storage"] = storage,
            ["price_list_name"] = list,
            ["price_id"] = priceId
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
