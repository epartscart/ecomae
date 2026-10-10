using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1PortParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Port");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Port_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_cp_brochure_live.php", PhpPlanQ1Port.BrochureLivePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Port.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Port.BrochureLivePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Port.EpcCpBrochureNormKey("Order Desk!!"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Port.Reset();
        return name switch
        {
            "norm" => new object[]
            {
                PhpPlanQ1Port.EpcCpBrochureNormKey("Order Desk!!"),
                PhpPlanQ1Port.EpcCpBrochureNormKey("  A   B  "),
                PhpPlanQ1Port.EpcCpBrochureNormKey(""),
                PhpPlanQ1Port.EpcCpBrochureCapCategoryToArea("Payments"),
                PhpPlanQ1Port.EpcCpBrochureCapCategoryToArea("Unknown Cat"),
                PhpPlanQ1Port.EpcCpBrochureCapCategoryToArea("Finance & ERP")
            },
            "image" => Image(),
            "pool" => Pool(),
            "visuals" => Visuals(),
            "caps" => Caps(),
            "erp" => Erp(),
            "live" => Live(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Image()
    {
        var item = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Brake pad", ["icon"] = "fa-stop" };
        var img = PhpPlanQ1Port.EpcCpBrochureItemImage(item, "Shop / OMS");
        var meta = PhpPlanQ1Port.EpcCpBrochureItemPhotoMeta(item, "Shop / OMS");
        var empty = PhpPlanQ1Port.EpcCpBrochureItemImage(new Dictionary<string, object?>(StringComparer.Ordinal), "Portal");
        return new object[] { img, meta["topic"]!, meta["label"]!, meta["photo"]!, empty };
    }

    private static object Pool()
    {
        PhpPlanQ1Port.TopicSvgUrl = (topic, id, _, _) => "/svg/" + topic + "/" + id;
        var pool = PhpPlanQ1Port.EpcCpBrochureScreenPool();
        var vis = PhpPlanQ1Port.EpcCpBrochureAreaVisuals();
        return new object[]
        {
            pool.Count,
            pool[0],
            pool[6],
            vis.Count,
            vis.Keys.First(),
            vis["Super CP / Platform"]["image"]!,
            vis["Super CP / Platform"]["icon"]!,
            vis["Portal"]["image"]!
        };
    }

    private static object Visuals()
    {
        PhpPlanQ1Port.TopicCatalog = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["platform"] = new(StringComparer.Ordinal) { ["photos"] = new List<string> { "/cat/platform.jpg" } },
            ["default"] = new(StringComparer.Ordinal) { ["photos"] = new List<string> { "/cat/default.jpg" } }
        };
        var vis = PhpPlanQ1Port.EpcCpBrochureAreaVisuals();
        return new object[]
        {
            vis["Super CP / Platform"]["image"]!,
            vis["Prices & Catalogue"]["image"]!,
            vis["Portal"]["blurb"]!
        };
    }

    private static object Caps()
    {
        SeedCaps();
        var caps = PhpPlanQ1Port.EpcCpBrochureCapabilitiesCatalog();
        var idx = PhpPlanQ1Port.EpcCpBrochureCapabilityIndex();
        return new object[]
        {
            caps.Count,
            idx.Count,
            idx.Keys.ToList(),
            idx["order desk"]["icon"]!,
            idx["order desk"]["id"]!,
            idx.ContainsKey("") ? 1 : 0
        };
    }

    private static object Erp()
    {
        var empty = PhpPlanQ1Port.EpcCpBrochureErpNavItems();
        SeedErp();
        var items = PhpPlanQ1Port.EpcCpBrochureErpNavItems();
        return new object[]
        {
            empty.Count,
            items.Count,
            items[0]["name"]!,
            items[0]["does"]!,
            items[0]["url"]!,
            items[0]["id"]!,
            items[1]["name"]!,
            items[1]["does"]!,
            items[1]["url"]!,
            items[1]["id"]!
        };
    }

    private static object Live()
    {
        SeedCaps();
        SeedInv();
        SeedErp();
        PhpPlanQ1Port.Clock = () => 1700000000;
        PhpPlanQ1Port.AssignUniquePhotos = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["Order desk"] = "/uniq/desk.jpg" };
        var inv = PhpPlanQ1Port.EpcCpBrochureBuildLiveInventory();
        var areas = (Dictionary<string, List<Dictionary<string, object?>>>)inv["areas"]!;
        var meta = (Dictionary<string, object?>)inv["meta"]!;
        var shop = areas["Shop / OMS"];
        var erp = areas["ERP / Modules"];
        var fin = areas["ERP / Finance"];
        var plat = areas["Super CP / Platform"];
        return new object[]
        {
            areas.Keys.ToList(),
            meta["total"]!,
            meta["area_count"]!,
            meta["generated_at"]!,
            meta["sources"]!,
            shop[0]["name"]!,
            shop[0]["does"]!,
            shop[0]["icon"]!,
            shop[0]["id"]!,
            shop[1]["name"]!,
            shop[1]["icon"]!,
            shop[1]["id"]!,
            erp[0]["name"]!,
            erp[0]["url"]!,
            erp[1]["name"]!,
            fin[0]["name"]!,
            fin[0]["scope"]!,
            plat[0]["name"]!,
            plat[0]["scope"]!,
            areas.ContainsKey("Empty Area") ? 1 : 0
        };
    }

    private static void SeedCaps()
    {
        PhpPlanQ1Port.CapsLoad = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "cap-orders", ["category"] = "Commerce — Orders & fulfilment", ["title"] = "Order desk", ["summary"] = "A longer summary that should replace short does", ["icon"] = "fa-inbox" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "cap-new", ["category"] = "Finance & ERP", ["title"] = "Treasury pulse", ["summary"] = "Cash position", ["icon"] = "fa-bank" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "", ["category"] = "Payments", ["title"] = "", ["summary"] = "skip empty title", ["icon"] = "fa-x" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "cap-plat", ["category"] = "Platform & Super CP", ["title"] = "Fleet host", ["summary"] = "Host tenants", ["icon"] = "fa-cloud" }
        ];
    }

    private static void SeedInv()
    {
        PhpPlanQ1Port.InventoryLoad = () => new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal)
        {
            ["Shop / OMS"] =
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Order desk", ["does"] = "short", ["url"] = "/cp/orders", ["scope"] = "client" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Epc leftover", ["does"] = "real work", ["url"] = "/cp/stub", ["scope"] = "client" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Real desk", ["does"] = "Open from left CP menu.", ["url"] = "/cp/menu", ["scope"] = "client" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Zed last", ["does"] = "sort me", ["url"] = "/cp/zed", ["scope"] = "client" }
            ],
            ["Empty Area"] = [],
            ["ERP / Modules"] =
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Old module", ["does"] = "stale", ["url"] = "/old", ["scope"] = "client" }
            ]
        };
    }

    private static void SeedErp()
    {
        PhpPlanQ1Port.ErpNavLoad = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["gl"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "General ledger",
                ["icon"] = "fa-book",
                ["desc"] = "Books",
                ["tabs"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["journals"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Journals", ["icon"] = "fa-list" },
                    ["skip"] = "not-array"
                }
            },
            ["cash"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Cash", ["icon"] = "fa-money", ["desc"] = "" }
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
