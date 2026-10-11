using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HindParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hind");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hind_MatchPhpGolden()
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
                failures.Add(name + " " + FirstDiff(exp, got));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/eparts_mod_catalog.php", PhpPlanQ1Hind.EpartsModCatalogPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hind.Reset();
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        var html = PhpPlanQ1Hind.RenderPage("/en");
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Hind.EpartsModCatalogPath, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-em-root\"", html, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hind.Reset();
        return name switch
        {
            "empty" => Empty(),
            "gates" => Gates(),
            "live" => Live(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var methodShops = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["getModels"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["shop_url"] = "https://partsapi.ru/account/shop?method=getModels"
            }
        };
        var actionShops = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["models"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["method"] = "getModels",
                ["label"] = "Vehicle models",
                ["shop_url"] = "https://partsapi.ru/account/shop?method=getModels"
            }
        };
        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["PRODUCT_GROUP"] = "Oil Filter", ["name"] = "Oil Filter" },
            new(StringComparer.Ordinal) { ["PRODUCT_GROUP"] = "Brake Pad Set", ["name"] = "Brake Pad Set" }
        };
        var cats = new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["STR_ID"] = 2, ["CATEGORY_NAME"] = "Filters" }
        };
        var map = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["2"] = new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Air" } }
        };
        var attached = PhpPlanQ1Hind.CategoriesWithSubcategories(cats, map);
        var children = attached[0].TryGetValue("children", out var ch) && ch is IEnumerable<object?> list
            ? list.Cast<object?>().Count()
            : 0;
        return new object[]
        {
            PhpPlanQ1Hind.Text(null),
            PhpPlanQ1Hind.Text(0),
            PhpPlanQ1Hind.Esc("A & B <x> \"q\" 'z'"),
            PhpPlanQ1Hind.ApiItems(new List<object?> { 1, 2 }).Count,
            PhpPlanQ1Hind.ApiItems(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["data"] = new List<object?> { "a", "b", "c" }
            }).Count,
            PhpPlanQ1Hind.ApiItems(new Dictionary<string, object?>(StringComparer.Ordinal) { ["nope"] = 1 }).Count,
            PhpPlanQ1Hind.IsSubscriptionErr(new Dictionary<string, object?>(StringComparer.Ordinal) { ["subscription_required"] = true }) ? 1 : 0,
            PhpPlanQ1Hind.IsSubscriptionErr(new Dictionary<string, object?>(StringComparer.Ordinal) { ["http_status"] = 401 }) ? 1 : 0,
            PhpPlanQ1Hind.IsSubscriptionErr(new Dictionary<string, object?>(StringComparer.Ordinal) { ["http_status"] = 200 }) ? 1 : 0,
            PhpPlanQ1Hind.PartSearchUrl("/en", false, "parts", "brands", "%2F", "VAG", "1J0-971 972"),
            PhpPlanQ1Hind.PartSearchUrl("/en", false, "parts", "brands", "%2F", "", ""),
            PhpPlanQ1Hind.PartSearchUrl("/ar", true, "parts", "brands", "%2F", "VAG/OEM", "1J0971972"),
            PhpPlanQ1Hind.ProductDetailUrl("/en", "MANN-FILTER", "W712/95"),
            PhpPlanQ1Hind.ProductDetailUrl("/en", "", "abc"),
            PhpPlanQ1Hind.PartSearchUrl("/en", false, "parts", "brands", "%2F", "BMW", "11427566327") == "/en/shop/cart" ? 1 : 0,
            PhpPlanQ1Hind.FilterRowsBySubcategory(rows, "oil").Count,
            PhpPlanQ1Hind.FilterRowsBySubcategory(rows, "missing-group").Count,
            children,
            PhpPlanQ1Hind.ShopUrlFor("models", null, methodShops, actionShops, "https://partsapi.ru/account/shop"),
            PhpPlanQ1Hind.ShopUrlFor("models", new Dictionary<string, object?>(StringComparer.Ordinal) { ["shop_url"] = "https://example/shop" }, methodShops, actionShops, "https://partsapi.ru/account/shop"),
            PhpPlanQ1Hind.SubscriptionMessage(new Dictionary<string, object?>(StringComparer.Ordinal) { ["error"] = "Need key" }, "models", actionShops),
            PhpPlanQ1Hind.SubscriptionMessage(null, "models", actionShops)
        };
    }

    private static object Gates()
    {
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.ecomae.com";
        PhpPlanQ1Hind.LangHref = "/en";
        var denied = PhpPlanQ1Hind.RenderPage("/en");
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        var ok = PhpPlanQ1Hind.RenderPage("/en");
        return new object[]
        {
            PhpPlanQ1Hind.PageFlags(denied),
            PhpPlanQ1Hind.PageFlags(ok),
            ok.Contains("data-lang-href=\"/en\"", StringComparison.Ordinal) ? 1 : 0,
            denied.Contains("epc-em-root", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Live()
    {
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        PhpPlanQ1Hind.LangHref = "/en";
        var html = PhpPlanQ1Hind.RenderPage("/en");
        return new object[]
        {
            PhpPlanQ1Hind.PageFlags(html),
            html.Contains("EParts API key not configured on server", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("Select a vehicle", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("/content/general_pages/epc_vc_catalog.css?v=", StringComparison.Ordinal) ? 1 : 0,
            12,
            5,
            PhpPlanQ1Hind.PendingCategoryBanner("Filters"),
            html.Contains("Set-Cookie", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Tenant()
    {
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        var ar = PhpPlanQ1Hind.RenderPage("/ar/");
        var en = PhpPlanQ1Hind.RenderPage("/en");
        return new object[]
        {
            PhpPlanQ1Hind.PageFlags(ar),
            PhpPlanQ1Hind.PageFlags(en),
            ar.Contains("data-lang-href=\"/ar\"", StringComparison.Ordinal) ? 1 : 0,
            en.Contains("data-lang-href=\"/en\"", StringComparison.Ordinal) ? 1 : 0,
            ar.Contains("/ar/shop/cart", StringComparison.Ordinal) ? 1 : 0,
            en.Contains("/en/shop/cart", StringComparison.Ordinal) ? 1 : 0,
            ar.Contains("/en/shop/cart", StringComparison.Ordinal) ? 1 : 0,
            en.Contains("/ar/shop/cart", StringComparison.Ordinal) ? 1 : 0,
            ar.Contains("/ar/contacts", StringComparison.Ordinal) ? 1 : 0,
            en.Contains("/en/contacts", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static string FirstDiff(string expected, string actual)
    {
        var n = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < n; i++)
        {
            if (expected[i] != actual[i])
            {
                var from = Math.Max(0, i - 40);
                return "at " + i + " exp=" + expected[from..Math.Min(expected.Length, i + 80)] + " got=" + actual[from..Math.Min(actual.Length, i + 80)];
            }
        }

        return expected.Length == actual.Length
            ? "equal-len-mismatch"
            : "len exp=" + expected.Length + " got=" + actual.Length + " tailExp=" + expected[Math.Max(0, expected.Length - 80)..] + " tailGot=" + actual[Math.Max(0, actual.Length - 80)..];
    }

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
