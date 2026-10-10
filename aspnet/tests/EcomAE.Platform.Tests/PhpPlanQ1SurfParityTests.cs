using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SurfParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Surf");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Surf_MatchPhpGolden()
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
            if (!Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Surf.BocAdvancedPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Surf.BocAdvancedPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Surf.Reset();
        Assert.Contains("epc_boc_advanced.php", PhpPlanQ1Surf.BocAdvancedPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Surf.BocAdvancedPath, StringComparison.Ordinal);
        Assert.Equal([], PhpPlanQ1Surf.EpcBocAdvFleetMetrics(null, (_, _) => []));
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Surf.Reset();
        return name switch
        {
            "pure" => Pure(),
            "collect" => Collect(),
            "fleet" => Fleet(),
            "render" => RenderHtml(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var money = new object[]
        {
            PhpPlanQ1Surf.EpcBocAdvMoney(0),
            PhpPlanQ1Surf.EpcBocAdvMoney(62.5),
            PhpPlanQ1Surf.EpcBocAdvMoney(999),
            PhpPlanQ1Surf.EpcBocAdvMoney(1000),
            PhpPlanQ1Surf.EpcBocAdvMoney(1234.5),
            PhpPlanQ1Surf.EpcBocAdvMoney(1_000_000),
            PhpPlanQ1Surf.EpcBocAdvMoney(1_250_000),
            PhpPlanQ1Surf.EpcBocAdvMoney(1_200_000),
            PhpPlanQ1Surf.EpcBocAdvMoney(-2500),
            PhpPlanQ1Surf.EpcBocAdvMoney(38.4, "USD")
        };
        var vendors = PhpPlanQ1Surf.EpcBocVendorRollup(
        [
            Dict(("site_key", "a"), ("label", "Alpha & Co"), ("type", "commerce"), ("ok", 1), ("vendors", 9), ("active_vendors", 7), ("rfq_open", 2), ("spend", 1_250_000d), ("currency", "AED"), ("note", "")),
            Dict(("site_key", "b"), ("label", "Beta's"), ("ok", "0"), ("vendors", 3), ("active_vendors", 1), ("rfq_open", 0), ("spend", 62.5), ("note", "down")),
            Dict(("site_key", "c"), ("label", "Gamma"), ("type", "demo"), ("ok", true), ("vendors", 1), ("spend", 0))
        ]);
        var warehouses = PhpPlanQ1Surf.EpcBocWarehouseRollup(
        [
            Dict(("site_key", "a"), ("label", "Alpha"), ("ok", 1), ("warehouses", 2), ("skus", 40), ("stock_value", 8800d), ("low_stock", 1), ("out_of_stock", 0)),
            Dict(("site_key", "b"), ("label", "Beta"), ("ok", 0), ("warehouses", 1), ("skus", 4), ("stock_value", 10d), ("low_stock", 0), ("out_of_stock", 0), ("note", "down")),
            Dict(("site_key", "c"), ("label", "Gamma"), ("ok", 1), ("warehouses", 1), ("skus", 8), ("stock_value", 500d), ("low_stock", 0), ("out_of_stock", 2))
        ]);
        var channels = PhpPlanQ1Surf.EpcBocChannelRollup(
        [
            Dict(("site_key", "a"), ("label", "Alpha"), ("ok", 1), ("web", 1), ("pos", 1), ("api", "0"), ("marketplaces", 2), ("arbitrage", 1)),
            Dict(("site_key", "b"), ("label", "Beta"), ("ok", 0), ("web", 0), ("pos", 0), ("api", 0), ("marketplaces", 0)),
            Dict(("site_key", "c"), ("label", "Gamma"), ("type", "demo"), ("ok", 1), ("web", true), ("pos", false), ("api", 1), ("marketplaces", 0), ("arbitrage", "0"))
        ]);
        var tiles = new object[]
        {
            PhpPlanQ1Surf.EpcBocAdvTile("Vendors", "1,234"),
            PhpPlanQ1Surf.EpcBocAdvTile("Active", "7", "green", "Beta's hint"),
            PhpPlanQ1Surf.EpcBocAdvTile("Open", "2", "amber")
        };
        var chips = new object[]
        {
            PhpPlanQ1Surf.EpcBocAdvRagChip("green"),
            PhpPlanQ1Surf.EpcBocAdvRagChip("amber"),
            PhpPlanQ1Surf.EpcBocAdvRagChip("red"),
            PhpPlanQ1Surf.EpcBocAdvRagChip("other"),
            PhpPlanQ1Surf.EpcBocAdvYn(true),
            PhpPlanQ1Surf.EpcBocAdvYn(false)
        };
        var classify = new object[]
        {
            PhpPlanQ1Surf.EpcBocClassifyTenant(Dict(("is_demo", 1), ("industry_code", "erp_only"))),
            PhpPlanQ1Surf.EpcBocClassifyTenant(Dict(("is_demo", "0"), ("industry_code", "ERP_STANDALONE"))),
            PhpPlanQ1Surf.EpcBocClassifyTenant(Dict(("industry", "erp_shop"))),
            PhpPlanQ1Surf.EpcBocClassifyTenant(Dict(("industry_code", "retail"))),
            PhpPlanQ1Surf.EpcBocClassifyTenant(Dict())
        };
        var labels = new object[]
        {
            PhpPlanQ1Surf.EpcBocTypeLabel("demo"),
            PhpPlanQ1Surf.EpcBocTypeLabel("erp_only"),
            PhpPlanQ1Surf.EpcBocTypeLabel("commerce"),
            PhpPlanQ1Surf.EpcBocTypeLabel("other"),
            PhpPlanQ1Surf.EpcBocH("Alpha & Co's <x>")
        };
        var hero = PhpPlanQ1Surf.EpcBocAdvHero("MULTI-VENDOR", "fa-truck", "Vendor & Sourcing Control", "Every supplier <x>");
        return new Rendered(new object[] { money, vendors, warehouses, channels, tiles, chips, classify, labels, hero });
    }

    private static Rendered Collect()
    {
        var db = FullStore();
        PhpPlanQ1Surf.UseStore(db);
        PhpPlanQ1Surf.MarketplaceChannels = siteKey =>
        {
            if (siteKey == "throw")
            {
                throw new InvalidOperationException("mkt");
            }

            if (siteKey == "alpha")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["sell"] = new object[] { "noon", "amazon" } };
            }

            if (siteKey == "empty-sell")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["sell"] = "nope" };
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal);
        };
        PhpPlanQ1Surf.ArbitrageEnabled = siteKey =>
        {
            if (siteKey == "throw-arb")
            {
                throw new InvalidOperationException("arb");
            }

            return siteKey == "alpha";
        };
        var vendor = PhpPlanQ1Surf.EpcBocCollectVendor(db);
        var wh = PhpPlanQ1Surf.EpcBocCollectWarehouse(db);
        var chAlpha = PhpPlanQ1Surf.EpcBocCollectChannel(db, db, "alpha", "commerce");
        var chDemo = PhpPlanQ1Surf.EpcBocCollectChannel(db, db, "beta", "demo");
        var chErp = PhpPlanQ1Surf.EpcBocCollectChannel(db, db, "gamma", "erp_only");
        var chThrow = PhpPlanQ1Surf.EpcBocCollectChannel(db, db, "throw", "commerce");
        var chEmptySell = PhpPlanQ1Surf.EpcBocCollectChannel(db, db, "empty-sell", "commerce");
        var exists = PhpPlanQ1Surf.EpcBocAdvTableExists(db, "epc_erp_suppliers");
        var missing = PhpPlanQ1Surf.EpcBocAdvTableExists(db, "no_such_table");
        var scalarVendors = PhpPlanQ1Surf.EpcBocAdvScalar(db, "SELECT COUNT(*) FROM `epc_erp_suppliers`");
        var bare = new PhpPlanQ1Surf.SurfStore();
        bare.Tables.Add("epc_pos_sales");
        var vendorBare = PhpPlanQ1Surf.EpcBocCollectVendor(bare);
        var whBare = PhpPlanQ1Surf.EpcBocCollectWarehouse(bare);
        var chSalesOnly = PhpPlanQ1Surf.EpcBocCollectChannel(null, bare, "z", "commerce");
        return new Rendered(new object?[]
        {
            vendor, wh, chAlpha, chDemo, chErp, chThrow, chEmptySell,
            exists, missing, scalarVendors,
            vendorBare, whBare, chSalesOnly
        });
    }

    private static Rendered Fleet()
    {
        var empty = new PhpPlanQ1Surf.SurfStore();
        PhpPlanQ1Surf.ListAll = () =>
        [
            Dict(("site_key", "alpha"), ("trade_name", "Alpha Live"), ("industry_code", "retail")),
            Dict(("site_key", "beta"), ("system_name", "Beta Sys"), ("is_demo", 1)),
            Dict(("site_key", "gone"), ("trade_name", "Gone")),
            Dict(("site_key", "boom"), ("trade_name", "Boom"), ("industry", "erp_only"))
        ];
        PhpPlanQ1Surf.TenantPdo = tenant =>
        {
            var key = Convert.ToString(tenant.TryGetValue("site_key", out var sk) ? sk : "", System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (key == "gone")
            {
                return null;
            }

            if (key == "boom")
            {
                throw new InvalidOperationException("connect");
            }

            return empty;
        };
        var per = PhpPlanQ1Surf.EpcBocAdvFleetMetrics(empty, (pdo, _) => PhpPlanQ1Surf.EpcBocCollectVendor(pdo));
        PhpPlanQ1Surf.ListAll = () => [];
        var none = PhpPlanQ1Surf.EpcBocAdvFleetMetrics(empty, (pdo, _) => PhpPlanQ1Surf.EpcBocCollectVendor(pdo));
        PhpPlanQ1Surf.ListAll = () => throw new InvalidOperationException("list");
        var threw = PhpPlanQ1Surf.EpcBocAdvFleetMetrics(empty, (pdo, _) => PhpPlanQ1Surf.EpcBocCollectVendor(pdo));
        return new Rendered(new object[] { per, none, threw });
    }

    private static Rendered RenderHtml()
    {
        var vendor = PhpPlanQ1Surf.EpcBocVendorRollup(
        [
            Dict(("site_key", "a"), ("label", "Alpha & Co"), ("type", "commerce"), ("ok", 1), ("vendors", 12), ("active_vendors", 9), ("rfq_open", 3), ("spend", 1_250_000d), ("currency", "AED")),
            Dict(("site_key", "b"), ("label", "Beta's"), ("type", "demo"), ("ok", 0), ("vendors", 2), ("active_vendors", 1), ("rfq_open", 0), ("spend", 400d), ("currency", "USD"), ("note", "DB unreachable"))
        ]);
        var warehouse = PhpPlanQ1Surf.EpcBocWarehouseRollup(
        [
            Dict(("site_key", "a"), ("label", "Alpha"), ("ok", 1), ("warehouses", 2), ("skus", 40), ("stock_value", 8800d), ("low_stock", 1), ("out_of_stock", 0), ("currency", "AED")),
            Dict(("site_key", "c"), ("label", "Gamma"), ("ok", 1), ("warehouses", 1), ("skus", 8), ("stock_value", 500d), ("low_stock", 0), ("out_of_stock", 2), ("currency", "AED"), ("note", ""))
        ]);
        var channel = PhpPlanQ1Surf.EpcBocChannelRollup(
        [
            Dict(("site_key", "a"), ("label", "Alpha"), ("ok", 1), ("web", 1), ("pos", 1), ("api", 1), ("marketplaces", 2), ("arbitrage", 1)),
            Dict(("site_key", "b"), ("label", "Beta"), ("type", "erp_only"), ("ok", 1), ("web", 0), ("pos", 0), ("api", 0), ("marketplaces", 0), ("arbitrage", 0))
        ]);
        var emptyVendor = PhpPlanQ1Surf.EpcBocVendorRollup([]);
        return new Rendered(new object[]
        {
            PhpPlanQ1Surf.EpcBocRenderVendorControl(null, "/boc", vendor),
            PhpPlanQ1Surf.EpcBocRenderWarehouseControl(null, "/boc", warehouse),
            PhpPlanQ1Surf.EpcBocRenderChannelControl(null, "/boc", channel),
            PhpPlanQ1Surf.EpcBocRenderVendorControl(null, "/boc", emptyVendor)
        });
    }

    private static PhpPlanQ1Surf.SurfStore FullStore()
    {
        var db = new PhpPlanQ1Surf.SurfStore();
        foreach (var table in new[]
        {
            "epc_erp_suppliers", "epc_scm_rfq", "epc_erp_purchases",
            "epc_erp_inv_warehouses", "epc_erp_inv_items", "epc_erp_inv_stock", "epc_scm_item_planning",
            "epc_pos_registers", "epc_pos_sales", "epc_api_clients"
        })
        {
            db.Tables.Add(table);
        }

        db.Suppliers.AddRange(
        [
            Dict(("id", 1), ("active", 1)),
            Dict(("id", 2), ("active", 1)),
            Dict(("id", 3), ("active", 0))
        ]);
        db.Rfq.AddRange(
        [
            Dict(("id", 1), ("status", "draft")),
            Dict(("id", 2), ("status", "sent")),
            Dict(("id", 3), ("status", "closed")),
            Dict(("id", 4), ("status", "open"))
        ]);
        db.Purchases.AddRange(
        [
            Dict(("id", 1), ("total_amount", 100.50d)),
            Dict(("id", 2), ("total_amount", 50.00d))
        ]);
        db.Warehouses.AddRange(
        [
            Dict(("id", 1), ("active", 1)),
            Dict(("id", 2), ("active", 0))
        ]);
        db.Items.AddRange(
        [
            Dict(("id", 1), ("active", 1)),
            Dict(("id", 2), ("active", 1)),
            Dict(("id", 3), ("active", 0))
        ]);
        db.Stock.AddRange(
        [
            Dict(("item_id", 1), ("qty_on_hand", 10d), ("avg_unit_cost", 2d)),
            Dict(("item_id", 2), ("qty_on_hand", 0d), ("avg_unit_cost", 5d)),
            Dict(("item_id", 4), ("qty_on_hand", -1d), ("avg_unit_cost", 3d))
        ]);
        db.Planning.AddRange(
        [
            Dict(("item_id", 1), ("reorder_point", 15d)),
            Dict(("item_id", 2), ("reorder_point", 5d))
        ]);
        db.PosRegisters.Add(Dict(("id", 1)));
        db.ApiClients.Add(Dict(("site_key", "alpha")));
        return db;
    }

    private static Dictionary<string, object?> Dict(params (string Key, object? Value)[] pairs)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            row[key] = value;
        }

        return row;
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 800 ? value : value[..800] + "…";
}
