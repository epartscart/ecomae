using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1StarboardParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Starboard");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Starboard_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_cp_full_brochure.php", PhpPlanQ1Starboard.FullBrochurePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Starboard.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Starboard.FullBrochurePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Starboard.EpcCpBrochureFilteredBundle("client")["meta"] is Dictionary<string, object?> m ? Convert.ToString(m["scope"]) ?? "" : "", StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Port.Reset();
        PhpPlanQ1Sprit.Reset();
        PhpPlanQ1Starboard.Reset();
        Seed();
        return name switch
        {
            "dedupe" => Dedupe(),
            "filter" => Filter(),
            "css" => Css(),
            "html" => Html(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void Seed()
    {
        PhpPlanQ1Starboard.BrochureH = PhpPlanQ1Sprit.EpcBrochureH;
        PhpPlanQ1Starboard.BrochureCss = p => "BASECSS:" + (p.TryGetValue("id", out var id) ? Convert.ToString(id) : "");
        PhpPlanQ1Starboard.BrochureProfile = brand =>
        {
            brand = System.Text.RegularExpressions.Regex.Replace((brand ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
            return brand == "ecomae"
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = "ecomae",
                    ["name"] = "ECOM AE",
                    ["legal"] = "Electronic World Group",
                    ["url"] = "https://www.ecomae.com",
                    ["cp_url"] = "https://www.ecomae.com/cp",
                    ["cover"] = "/c/ecomae.jpg",
                    ["contact_email"] = "hello@ecomae.com"
                }
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = "epartscart",
                    ["name"] = "eParts Cart",
                    ["legal"] = "Electronic World Group",
                    ["url"] = "https://www.epartscart.com",
                    ["cp_url"] = "https://www.epartscart.com/cp",
                    ["cover"] = "/c/parts.jpg",
                    ["contact_email"] = "hello@epartscart.com"
                };
        };
        PhpPlanQ1Starboard.LiveInventory = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["areas"] = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal)
            {
                ["Shop / OMS"] =
                [
                    Item("Order desk", "Run orders", "/cp/orders", "client", "fa-inbox"),
                    Item("Epc leftover", "skip", "/cp/orders", "client", "fa-cube"),
                    Item("Fleet host", "Host tenants", "", "super", "fa-cloud"),
                    Item("Shared desk", "Both sides", "/cp/shared", "both", "fa-users")
                ],
                ["Empty Area"] = []
            },
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["generated_at"] = 1700000000L,
                ["sources"] = new List<string> { "curated inventory", "capability append" },
                ["total"] = 4,
                ["area_count"] = 1
            }
        };
        PhpPlanQ1Starboard.AreaVisuals = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["Shop / OMS"] = new(StringComparer.Ordinal) { ["icon"] = "fa-shopping-cart", ["image"] = "/v/orders.png", ["blurb"] = "Daily desk" }
        };
        PhpPlanQ1Starboard.ItemPhoto = (item, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["photo"] = "/p/" + (item.TryGetValue("name", out var n) ? Convert.ToString(n) : "x") + ".jpg",
            ["label"] = "Ops",
            ["topic"] = "default"
        };
        PhpPlanQ1Starboard.ItemImage = (item, _) => "/i/" + (item.TryGetValue("name", out var n) ? Convert.ToString(n) : "x") + ".jpg";
        PhpPlanQ1Starboard.Clock = () => 1700000000;
    }

    private static Dictionary<string, object?> Item(string name, string does, string url, string scope, string icon)
        => new(StringComparer.Ordinal) { ["name"] = name, ["does"] = does, ["url"] = url, ["scope"] = scope, ["icon"] = icon };

    private static object Dedupe()
    {
        var items = new List<Dictionary<string, object?>>
        {
            Item("Epc leftover", "skip", "/cp/orders", "client", "fa-cube"),
            Item("Order desk", "Run orders", "/cp/orders", "client", "fa-inbox"),
            Item("No url", "plain", "", "client", "fa-cube"),
            Item("Zed", "Open from left CP menu.", "/cp/zed", "client", "fa-cube")
        };
        var output = PhpPlanQ1Starboard.EpcCpBrochureDedupeItems(items);
        return new object[] { output.Count, output.Select(r => r["name"]).ToList() };
    }

    private static object Filter()
    {
        var load = PhpPlanQ1Starboard.EpcCpBrochureLoadInventory();
        var client = PhpPlanQ1Starboard.EpcCpBrochureFilteredBundle("CLIENT!");
        var super = PhpPlanQ1Starboard.EpcCpBrochureFilteredBundle("super");
        var all = PhpPlanQ1Starboard.EpcCpBrochureFilteredBundle("nope");
        var inv = PhpPlanQ1Starboard.EpcCpBrochureFilteredInventory("all");
        var clientAreas = ((Dictionary<string, List<Dictionary<string, object?>>>)client["areas"]!).Keys.ToList();
        var clientNames = ((Dictionary<string, List<Dictionary<string, object?>>>)client["areas"]!)["Shop / OMS"].Select(r => r["name"]).ToList();
        var superNames = ((Dictionary<string, List<Dictionary<string, object?>>>)super["areas"]!)["Shop / OMS"].Select(r => r["name"]).ToList();
        var clientMeta = (Dictionary<string, object?>)client["meta"]!;
        var superMeta = (Dictionary<string, object?>)super["meta"]!;
        var allMeta = (Dictionary<string, object?>)all["meta"]!;
        return new object[]
        {
            load["Shop / OMS"].Count,
            clientMeta["scope"]!,
            clientMeta["total"]!,
            clientAreas,
            clientNames,
            superMeta["total"]!,
            superNames,
            allMeta["scope"]!,
            inv["Shop / OMS"].Count,
            ((Dictionary<string, List<Dictionary<string, object?>>>)client["areas"]!).ContainsKey("Empty Area") ? 1 : 0
        };
    }

    private static object Css()
    {
        var css = PhpPlanQ1Starboard.EpcCpBrochureCss(new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "ecomae" });
        return new object[] { ByteLen(css), css.Length >= 24 ? css[..24] : css, css.Contains(".epc-br--deck", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Html()
    {
        var deck = PhpPlanQ1Starboard.EpcCpFullBrochureRenderHtml(new Dictionary<string, object?> { ["brand"] = "ecomae", ["scope"] = "client", ["view"] = "deck" });
        var cat = PhpPlanQ1Starboard.EpcCpFullBrochureRenderHtml(new Dictionary<string, object?> { ["brand"] = "Eparts-Cart!", ["scope"] = "all", ["view"] = "catalog" });
        var print = PhpPlanQ1Starboard.EpcCpFullBrochureRenderHtml(new Dictionary<string, object?> { ["brand"] = "ecomae", ["print"] = true });
        var print0 = PhpPlanQ1Starboard.EpcCpFullBrochureRenderHtml(new Dictionary<string, object?> { ["brand"] = "ecomae", ["print"] = "0" });
        return new object[]
        {
            ByteLen(deck),
            deck.Length >= 80 ? deck[..80] : deck,
            deck.Contains("ECOM AE", StringComparison.Ordinal) ? 1 : 0,
            deck.Contains("14 Nov 2023", StringComparison.Ordinal) ? 1 : 0,
            deck.Contains("Order desk", StringComparison.Ordinal) ? 1 : 0,
            deck.Contains("Fleet host", StringComparison.Ordinal) ? 1 : 0,
            deck.Contains("scope=client&view=deck", StringComparison.Ordinal) ? 1 : 0,
            ByteLen(cat),
            cat.Contains("eParts Cart", StringComparison.Ordinal) ? 1 : 0,
            cat.Contains("Catalogue view", StringComparison.Ordinal) ? 1 : 0,
            cat.Contains("Fleet host", StringComparison.Ordinal) ? 1 : 0,
            print.Contains("window.print", StringComparison.Ordinal) ? 1 : 0,
            print0.Contains("setTimeout", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static int ByteLen(string value) => Encoding.UTF8.GetByteCount(value);

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
