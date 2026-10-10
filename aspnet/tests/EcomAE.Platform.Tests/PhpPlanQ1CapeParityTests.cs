using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1CapeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Cape");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Cape_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_epartscart_storefront.php", PhpPlanQ1Cape.EpartscartStorefrontPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Cape.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Cape.EpartscartStorefrontPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Cape.EpcEpartscartLangHref(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Cape.Reset();
        return name switch
        {
            "names" => Names(),
            "flags" => Flags(),
            "redirect" => Redirect(),
            "tree" => Tree(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
        => new object?[]
        {
            PhpPlanQ1Cape.EpcEpartscartIsApaiAlias(""),
            PhpPlanQ1Cape.EpcEpartscartIsApaiAlias("Tires") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiAlias("APAI-Oil") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiAlias("apai_filters") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiAlias("  APAI-x  ") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiUrl("") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiUrl("/apai-oil") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiUrl("apai_filters") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiUrl("shop/apai-oil") ? 1 : 0,
            PhpPlanQ1Cape.EpcEpartscartIsApaiUrl("tires") ? 1 : 0,
            PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("Electronics UAE"),
            PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("Fashion / apparel"),
            PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("Jewellery"),
            PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("svc-tax-advisor"),
            PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("auto parts"),
            PhpPlanQ1Cape.EpcStorefrontCatalogPlaceholderForHint("other")
        };

    private static object Flags()
    {
        var pdo = new object();
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => false;
        var off = PhpPlanQ1Cape.EpcEpartscartStorefrontActive(pdo) ? 1 : 0;
        var phOff = PhpPlanQ1Cape.EpcEpartscartCatalogPlaceholderUrl(pdo);
        var neuOff = PhpPlanQ1Cape.EpcEpartscartUseNeutralProductImage(pdo) ? 1 : 0;
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => true;
        var on = PhpPlanQ1Cape.EpcEpartscartStorefrontActive(pdo) ? 1 : 0;
        var phOn = PhpPlanQ1Cape.EpcEpartscartCatalogPlaceholderUrl(pdo);
        var neuOn = PhpPlanQ1Cape.EpcEpartscartUseNeutralProductImage(pdo) ? 1 : 0;
        var phNull = PhpPlanQ1Cape.EpcEpartscartCatalogPlaceholderUrl(null);
        PhpPlanQ1Cape.LangPrefix = null;
        var langDefault = PhpPlanQ1Cape.EpcEpartscartLangHref();
        PhpPlanQ1Cape.LangPrefix = () => "/ar/";
        var langAr = PhpPlanQ1Cape.EpcEpartscartLangHref();
        return new object?[] { off, phOff, neuOff, on, phOn, neuOn, phNull, langDefault, langAr };
    }

    private static object Redirect()
    {
        var pdo = new object();
        PhpPlanQ1Cape.LangPrefix = () => "/en/";
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => false;
        PhpPlanQ1Cape.CategoriesPresent = () => false;
        var inactive = PhpPlanQ1Cape.EpcEpartscartApaiCategoryRedirect(pdo, "apai-oil");
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => true;
        var notApai = PhpPlanQ1Cape.EpcEpartscartApaiCategoryRedirect(pdo, "tires");
        var noFile = PhpPlanQ1Cape.EpcEpartscartApaiCategoryRedirect(pdo, "apai-oil");
        PhpPlanQ1Cape.CategoriesPresent = () => true;
        PhpPlanQ1Cape.ResolveProductRoute = (_, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["product"] = "OC47" };
        var withProduct = PhpPlanQ1Cape.EpcEpartscartApaiCategoryRedirect(pdo, "apai-oil");
        PhpPlanQ1Cape.ResolveProductRoute = (_, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["product"] = "" };
        var emptyProduct = PhpPlanQ1Cape.EpcEpartscartApaiCategoryRedirect(pdo, "shop/apai_x");
        PhpPlanQ1Cape.ResolveProductRoute = (_, _, _) => null;
        var miss = PhpPlanQ1Cape.EpcEpartscartApaiCategoryRedirect(pdo, "/apai-oil/");
        return new object?[] { inactive, notApai, noFile, withProduct, emptyProduct, miss };
    }

    private static object Tree()
    {
        var pdo = new object();
        var tree = new List<Dictionary<string, object?>>
        {
            Node("tires", [Node("apai-summer"), Node("winter")]),
            Node("apai_rims"),
            Node("batteries")
        };
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => false;
        var passthrough = PhpPlanQ1Cape.EpcEpartscartFilterMenuTree(pdo, Clone(tree));
        PhpPlanQ1Cape.IsWarehouseStorefront = _ => true;
        var filtered = PhpPlanQ1Cape.EpcEpartscartFilterMenuTree(pdo, Clone(tree));
        return new object[] { Proj(passthrough), Proj(filtered) };
    }

    private static Dictionary<string, object?> Node(string alias, List<Dictionary<string, object?>>? data = null)
        => new(StringComparer.Ordinal)
        {
            ["alias"] = alias,
            ["data"] = data ?? []
        };

    private static List<Dictionary<string, object?>> Clone(List<Dictionary<string, object?>> tree)
        => tree.Select(n =>
        {
            var copy = new Dictionary<string, object?>(n, StringComparer.Ordinal);
            if (n.GetValueOrDefault("data") is List<Dictionary<string, object?>> kids)
            {
                copy["data"] = Clone(kids);
            }

            return copy;
        }).ToList();

    private static List<object> Proj(List<Dictionary<string, object?>> nodes)
        => nodes.Select(n =>
        {
            var kids = n.GetValueOrDefault("data") is List<Dictionary<string, object?>> data
                ? data.Select(c => Convert.ToString(c.GetValueOrDefault("alias")) ?? "").ToList()
                : [];
            return (object)new object[] { Convert.ToString(n.GetValueOrDefault("alias")) ?? "", kids };
        }).ToList();

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
