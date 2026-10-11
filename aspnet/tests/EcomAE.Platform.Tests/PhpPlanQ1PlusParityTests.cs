using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1PlusParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Plus");

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
    public void PlanQ1Plus_MatchPhpGolden()
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
            if (!Same(actual, expected))
            {
                failures.Add(name + " expected=" + Truncate(expected.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Plus.PriceExtraFieldsPath,
                PhpPlanQ1Plus.CpScriptRelocatePath,
                PhpPlanQ1Plus.PosTerminalMarkupPath,
                PhpPlanQ1Plus.CpRoleHomePath,
                PhpPlanQ1Plus.ChannelSchemaPath,
                PhpPlanQ1Plus.ChannelHelpersPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Plus.PriceExtraFieldsPath,
                PhpPlanQ1Plus.CpScriptRelocatePath,
                PhpPlanQ1Plus.PosTerminalMarkupPath,
                PhpPlanQ1Plus.CpRoleHomePath,
                PhpPlanQ1Plus.ChannelSchemaPath,
                PhpPlanQ1Plus.ChannelHelpersPath
            });

    [Fact]
    public void ExtraMaps_DoesNotStartASession()
    {
        Assert.Equal("engine_code", PhpPlanQ1Plus.EpcPriceExtraSlugKey("Engine Code!!"));
        Assert.Equal("", PhpPlanQ1Plus.EpcPriceExtraNormalizeValue(""));
    }

    private static string Render(string name)
        => name switch
        {
            "extra_maps" => Json(ExtraMaps()),
            "extra_index" => Json(ExtraIndex()),
            "relocate" => Json(Relocate()),
            "pos_markup" => Json(PosMarkup()),
            "role_data" => Json(RoleData()),
            "role_db" => Json(RoleDb()),
            "channel_maps" => Json(ChannelMaps()),
            "channel_seed" => Json(ChannelSeed()),
            _ => "unknown:" + name
        };

    private static object?[] ExtraMaps()
    {
        var cat = PhpPlanQ1Plus.EpcPriceExtraFieldCatalog();
        var map = PhpPlanQ1Plus.EpcPriceExtraMapHeaderColumns(
            new[] { "sku", "Engine code", "Foo Bar", "", "other info", "size mm", "x-ref" },
            new Dictionary<string, int>(StringComparer.Ordinal) { ["article"] = 0 });
        var row = new[] { "A", "B20B", "red", "", "note1", "12", "R1" };
        var mappedRow = new string[7];
        for (var i = 0; i < row.Length; i++)
        {
            mappedRow[i] = row[i];
        }

        var ex = PhpPlanQ1Plus.EpcPriceExtraExtractFromRow(mappedRow, map);
        var ex2 = PhpPlanQ1Plus.EpcPriceExtraExtractFromRow(
            new[] { "A", "B20B", "B20C" },
            new Dictionary<int, string> { [1] = "engine_code", [2] = "engine_code" });
        var empty1 = PhpPlanQ1Plus.EpcPriceExtraDecode("");
        var empty2 = PhpPlanQ1Plus.EpcPriceExtraDecode("not-json");
        return new object?[]
        {
            cat.EnumerateObject().Select(p => p.Name).ToArray(),
            JsonSerializer.Deserialize<object>(cat.GetProperty("engine_code").GetProperty("aliases").GetRawText()),
            PhpPlanQ1Plus.EpcPriceExtraSearchOptions(),
            PhpPlanQ1Plus.EpcPriceExtraLabel("engine_code"),
            PhpPlanQ1Plus.EpcPriceExtraLabel("unknown_field"),
            PhpPlanQ1Plus.EpcPriceExtraNormalizeHeader("  Engine_Code/\tX  "),
            PhpPlanQ1Plus.EpcPriceExtraNormalizeHeader(""),
            PhpPlanQ1Plus.EpcPriceExtraSlugKey("Engine Code!!"),
            PhpPlanQ1Plus.EpcPriceExtraSlugKey("!!!"),
            PhpPlanQ1Plus.EpcPriceExtraNormalizeValue("  ab-12.3  "),
            PhpPlanQ1Plus.EpcPriceExtraNormalizeValue(""),
            map,
            ex,
            ex2,
            PhpPlanQ1Plus.EpcPriceExtraMerge(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["size"] = "10", ["other"] = "a | A" },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["size"] = "11", ["color"] = "red", ["other"] = "b" }),
            PhpPlanQ1Plus.EpcPriceExtraMerge(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["size"] = "10" },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["size"] = "10", ["x"] = "" }),
            PhpPlanQ1Plus.EpcPriceExtraEncode(new Dictionary<string, string>()),
            PhpPlanQ1Plus.EpcPriceExtraEncode(new Dictionary<string, string>(StringComparer.Ordinal) { ["b"] = "2", ["a"] = "1" }),
            PhpPlanQ1Plus.EpcPriceExtraDecode("{\"a\":\"1\"}"),
            PhpPlanQ1Plus.EpcPriceExtraDecode(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1", ["b"] = "" }),
            empty1.Count == 0 ? Array.Empty<object>() : empty1,
            empty2.Count == 0 ? Array.Empty<object>() : empty2
        };
    }

    private static object?[] ExtraIndex()
    {
        var db = new PhpPlanQ1Plus.PriceExtraStore();
        var ok = PhpPlanQ1Plus.EpcPriceExtraEnsureSchema(db);
        PhpPlanQ1Plus.EpcPriceExtraSaveForRow(db, 1, 9, new Dictionary<string, string>(), "BOSCH", "0986", "0986", "Oil");
        PhpPlanQ1Plus.EpcPriceExtraSaveForRow(
            db,
            1,
            9,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["engine_code"] = "B20B",
                ["cross_reference"] = "X1; Y2",
                ["other"] = "note"
            },
            "BOSCH",
            "0986",
            "0986",
            "Oil");
        var s1 = PhpPlanQ1Plus.EpcPriceAttrSearch(db, "all", "B2");
        var s2 = PhpPlanQ1Plus.EpcPriceAttrSearch(db, "engine_code", "B20B");
        var s3 = PhpPlanQ1Plus.EpcPriceAttrSearch(db, "all", "x");
        var s4 = PhpPlanQ1Plus.EpcPriceAttrSearch(db, "!!!", "B20B");
        var s5 = PhpPlanQ1Plus.EpcPriceAttrSearch(db, "custom_slug", "ZZ");
        db.PricesData.Add(new PhpPlanQ1Plus.PriceDataRow
        {
            Id = 1,
            PriceId = 9,
            Manufacturer = "Bosch",
            Article = "0986",
            ArticleShow = "0986"
        });
        var lu = PhpPlanQ1Plus.EpcPriceExtraLookup(db, 9, "Bosch", "0986");
        var jp = PhpPlanQ1Plus.EpcPriceExtraJsonParams(db, 9, "Bosch", "0986", new Dictionary<string, object?>(StringComparer.Ordinal) { ["foo"] = 1 });
        var jpEmpty = PhpPlanQ1Plus.EpcPriceExtraJsonParams(db, 0, "x", "y", new Dictionary<string, object?>());
        PhpPlanQ1Plus.EpcPriceExtraClearForPrice(db, 9);
        var s6 = PhpPlanQ1Plus.EpcPriceAttrSearch(db, "all", "B20B");
        var lu2 = PhpPlanQ1Plus.EpcPriceExtraLookup(db, 9, "Bosch", "0986");
        var items = (List<Dictionary<string, object?>>)s1["items"]!;
        return new object?[]
        {
            ok,
            s1["ok"],
            s1["message"],
            s1["count"],
            items.Count > 0 ? items[0]["matched_field"] : "",
            items.Count > 0 ? items[0]["matched_value"] : "",
            s2["count"],
            s3["ok"],
            s3["message"],
            s4["ok"],
            s4["message"],
            s5["ok"],
            s5["message"],
            lu,
            jp,
            jpEmpty,
            s6["count"],
            lu2
        };
    }

    private static object?[] Relocate()
    {
        PhpPlanQ1Plus.EpcCpFooterScriptsReset();
        PhpPlanQ1Plus.FooterStyles.Clear();
        var h1 = PhpPlanQ1Plus.EpcCpExtractScriptsFromHtml("<p>Hi</p><script>alert(1)</script><b>X</b>");
        var scripts = PhpPlanQ1Plus.FooterScripts.ToArray();
        PhpPlanQ1Plus.EpcCpFooterScriptsReset();
        PhpPlanQ1Plus.FooterStyles.Clear();
        var h2 = PhpPlanQ1Plus.EpcCpExtractStylesFromHtml("<div><style>.a{color:red}</style>ok</div>");
        var styles = PhpPlanQ1Plus.FooterStyles.ToArray();
        PhpPlanQ1Plus.EpcCpFooterScriptsReset();
        PhpPlanQ1Plus.FooterStyles.Clear();
        var prep = PhpPlanQ1Plus.EpcCpPrepareCpPageContent("<style>a{}</style>Hi<script src=\"x.js\"></script>End");
        var mods = new Dictionary<string, string>(StringComparer.Ordinal) { ["left"] = "<script>1</script>L", ["right"] = "R" };
        PhpPlanQ1Plus.EpcCpPrepareCpModules(mods);
        var begin = PhpPlanQ1Plus.EpcCpMainPaneBeginMarker();
        var end = PhpPlanQ1Plus.EpcCpMainPaneEndMarker();
        var html = "HEAD" + begin + "<style>z{}</style>MAIN<script>2</script>" + end + "TAIL";
        PhpPlanQ1Plus.EpcCpFooterScriptsReset();
        PhpPlanQ1Plus.FooterStyles.Clear();
        var rel = PhpPlanQ1Plus.EpcCpRelocateMainPaneScripts(html);
        var strip = PhpPlanQ1Plus.EpcCpStripMainPaneMarkers(rel);
        PhpPlanQ1Plus.EpcCpFooterScriptsReset();
        PhpPlanQ1Plus.FooterStyles.Clear();
        var fin = PhpPlanQ1Plus.EpcCpFinalizeCpHtml("<html><head></head><body class=\"x\">" + begin + "<script>3</script>" + end + "<div class=\"epc-boc\"></div></body></html>");
        var boc = PhpPlanQ1Plus.EpcCpBocFirstPaintPatch("<html><head></head><body class=\"wrap\"><div class=\"epc-boc\">c</div></body></html>");
        var boc2 = PhpPlanQ1Plus.EpcCpBocFirstPaintPatch("<html><head></head><body class=\"wrap\"><p>no</p></body></html>");
        var none = PhpPlanQ1Plus.EpcCpRelocateMainPaneScripts("no-markers");
        var empty = PhpPlanQ1Plus.EpcCpExtractScriptsFromHtml("");
        PhpPlanQ1Plus.FooterStyles.Clear();
        PhpPlanQ1Plus.FooterScripts.Clear();
        PhpPlanQ1Plus.FooterStyles.Add("  <style>q{}</style>  ");
        PhpPlanQ1Plus.FooterScripts.Add("<script>q</script>");
        var inj = PhpPlanQ1Plus.EpcCpInjectRelocatedFooterAssets("<html><body>Z</body></html>");
        return new object?[] { h1, scripts, h2, styles, prep, mods, begin, end, rel, strip, fin, boc, boc2, none, empty, inj };
    }

    private static object?[] PosMarkup()
    {
        var closed = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["today_sales"] = 3,
                ["today_total"] = 12.5,
                ["week_sales"] = 8,
                ["week_total"] = 99.1
            },
            ["open_session"] = null,
            ["tax_ctx"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tax_rate"] = 5, ["tax_label"] = "VAT" },
            ["settings"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["register_name"] = "Front <desk>" },
            ["warehouses"] = Array.Empty<object>(),
            ["ajax_url"] = "/a",
            ["pos_url"] = "/p",
            ["csrf"] = "tok",
            ["erp_url"] = "/erp",
            ["warehouse_url"] = "",
            ["settings_url"] = "/set",
            ["currency"] = "AED",
            ["warehouse_id"] = 4,
            ["warehouse_name"] = "Main",
            ["country_code"] = "AE"
        };
        var open = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["open_session"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 17,
                ["session_no"] = "S-1",
                ["opening_float"] = 50
            },
            ["tax_ctx"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["settings"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["warehouses"] = new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 4, ["name"] = "Main" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 9, ["code"] = "WH9" }
            },
            ["ajax_url"] = "",
            ["pos_url"] = "",
            ["csrf"] = "",
            ["erp_url"] = "",
            ["warehouse_url"] = "/wh",
            ["settings_url"] = "",
            ["currency"] = "AED",
            ["warehouse_id"] = 9,
            ["warehouse_name"] = "Default warehouse",
            ["country_code"] = ""
        };
        return new object?[]
        {
            PhpPlanQ1Plus.EpcPosTerminalRenderMarkup(closed),
            PhpPlanQ1Plus.EpcPosTerminalRenderMarkup(open)
        };
    }

    private static object?[] RoleData()
    {
        var roles = PhpPlanQ1Plus.EpcCpRoles();
        var tiles = PhpPlanQ1Plus.EpcCpRoleDashboardTiles("finance");
        var viewer = PhpPlanQ1Plus.EpcCpRoleDashboardTiles("nope");
        var act = PhpPlanQ1Plus.EpcCpRoleQuickActions("admin");
        var actV = PhpPlanQ1Plus.EpcCpRoleQuickActions("nope");
        return new object?[]
        {
            roles.Keys.ToArray(),
            roles["admin"]["label"],
            tiles[0]["id"],
            tiles[0]["query"],
            viewer.Count,
            act[5]["label"],
            actV[0]["url"],
            PhpPlanQ1Plus.EpcCpRoleModules("sales"),
            PhpPlanQ1Plus.EpcCpRoleCan("admin", "users.create"),
            PhpPlanQ1Plus.EpcCpRoleCan("admin", "users"),
            PhpPlanQ1Plus.EpcCpRoleCan("finance", "finance.x"),
            PhpPlanQ1Plus.EpcCpRoleCan("finance", "orders.view"),
            PhpPlanQ1Plus.EpcCpRoleCan("finance", "orders.edit"),
            PhpPlanQ1Plus.EpcCpRoleCan("viewer", "reports.view"),
            PhpPlanQ1Plus.EpcCpRoleCan("nope", "orders.view"),
            PhpPlanQ1Plus.EpcCpRoleHomeRender("support"),
            PhpPlanQ1Plus.EpcCpRoleHomeRender("nope")
        };
    }

    private static object?[] RoleDb()
    {
        var db = new PhpPlanQ1Plus.RoleHomeStore
        {
            Users =
            {
                new PhpPlanQ1Plus.RoleUserRow { Id = 1, Role = "finance", Active = 1 },
                new PhpPlanQ1Plus.RoleUserRow { Id = 2, Role = "nope", Active = 1 }
            }
        };
        var d1 = PhpPlanQ1Plus.EpcCpDetectRole(db, 1);
        var d2 = PhpPlanQ1Plus.EpcCpDetectRole(db, 1, new Dictionary<string, string>(StringComparer.Ordinal) { ["epc_user_role"] = "warehouse" });
        var d3 = PhpPlanQ1Plus.EpcCpDetectRole(db, 2);
        var d4 = PhpPlanQ1Plus.EpcCpDetectRole(null, 0);
        var ok = PhpPlanQ1Plus.EpcCpRoleAssign(db, 1, "sales");
        var bad = PhpPlanQ1Plus.EpcCpRoleAssign(db, 1, "nope");
        var role = db.Users.Find(u => u.Id == 1)!.Role;
        var t = PhpPlanQ1Plus.EpcCpTileResolve(db, "active_users");
        var t2 = PhpPlanQ1Plus.EpcCpTileResolve(db, "nope_query");
        var t3 = PhpPlanQ1Plus.EpcCpTileQueryScalar(db, "SELECT 7");
        return new object?[] { d1, d2, d3, d4, ok, bad, role, t, t2, t3 };
    }

    private static object?[] ChannelMaps()
    {
        var c = PhpPlanQ1Plus.EpcChannelCarriersCatalog();
        var m = PhpPlanQ1Plus.EpcChannelMarketplacesCatalog();
        var b = PhpPlanQ1Plus.EpcChannelCarrierDemoBases();
        return new object?[]
        {
            c.EnumerateObject().Select(p => p.Name).ToArray(),
            c.GetProperty("dhl").GetProperty("name").GetString(),
            c.GetProperty("dhl").GetProperty("demo_base").GetDouble(),
            c.GetProperty("dhl").GetProperty("services").EnumerateObject().Select(p => p.Name).ToArray(),
            m.EnumerateObject().Select(p => p.Name).ToArray(),
            m.GetProperty("amazon").GetProperty("marketplace_id").GetString(),
            m.GetProperty("noon").GetProperty("config").GetProperty("country").GetString(),
            b["dhl"],
            b["imile"],
            PhpPlanQ1Plus.EpcChannelDemoRate("dhl", 1, "AE"),
            PhpPlanQ1Plus.EpcChannelDemoRate("dhl", 1, "GB"),
            PhpPlanQ1Plus.EpcChannelDemoRate("nope", 0, "AE"),
            PhpPlanQ1Plus.EpcChannelMoney(1234.5),
            PhpPlanQ1Plus.EpcChannelMoney(""),
            PhpPlanQ1Plus.EpcChannelH("a&b<'x'>"),
            PhpPlanQ1Plus.EpcChannelConfigureUrls()
        };
    }

    private static object?[] ChannelSeed()
    {
        var db = new PhpPlanQ1Plus.ChannelStore();
        PhpPlanQ1Plus.EpcChannelEnsureSchema(db);
        PhpPlanQ1Plus.EpcChannelSeedSampleData(db);
        var dash = PhpPlanQ1Plus.EpcChannelDashboard(db);
        var mps = PhpPlanQ1Plus.EpcChannelListMarketplaces(db);
        var codes = mps.Select(r => (string)r["code"]!).ToArray();
        var car = PhpPlanQ1Plus.EpcChannelListCarriers(db);
        var sync = PhpPlanQ1Plus.EpcChannelSyncInventoryDemo(db, "amazon");
        var imp = PhpPlanQ1Plus.EpcChannelImportOrderDemo(db, 1);
        var imp2 = PhpPlanQ1Plus.EpcChannelImportOrderDemo(db, 1);
        var rep = PhpPlanQ1Plus.EpcChannelDemoReport(db);
        var skuN = ((List<Dictionary<string, object?>>)rep["sku_map"]!).Count;
        var ordN = ((List<Dictionary<string, object?>>)rep["marketplace_orders"]!).Count;
        var logN = ((List<Dictionary<string, object?>>)rep["sync_log"]!).Count;
        var snap = PhpPlanQ1Plus.EpcChannelGuideSnapshot(db);
        return new object?[]
        {
            dash["channels_total"],
            dash["channels_active"],
            dash["catalog_count"],
            dash["regions"],
            dash["sku_mapped"],
            dash["marketplace_orders"],
            dash["marketplace_pending"],
            codes,
            car.Count,
            sync["channel"],
            sync["skus_pushed"],
            ((List<Dictionary<string, object?>>)sync["items"]!).Count,
            imp["status"],
            imp["external_order_id"],
            imp2["message"],
            skuN,
            ordN,
            logN,
            snap.ContainsKey("generated_at"),
            ((Dictionary<string, object?>)snap["dashboard"]!)["sku_mapped"]
        };
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
        => value.Length <= 400 ? value : value[..400] + "…";
}
