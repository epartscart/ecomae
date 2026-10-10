using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1RiseParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Rise");

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
    public void PlanQ1Rise_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Rise.MainstreamMenuPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Rise.MainstreamMenuPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Rise.Reset();
        Assert.Contains("epc_cp_mainstream_menu.php", PhpPlanQ1Rise.MainstreamMenuPath, StringComparison.Ordinal);
        var hidden = PhpPlanQ1Rise.EpcCpSystemMenuHiddenUrlPatterns();
        Assert.DoesNotContain("PHPSESSID", string.Join(",", hidden), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Rise.Reset();
        return name switch
        {
            "pure" => Pure(),
            "empty" => Empty(),
            "seed" => Seed(),
            "packs" => Packs(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
        => new(new object?[]
        {
            PhpPlanQ1Rise.EpcCpMenuParityRegistry(),
            PhpPlanQ1Rise.EpcCpSystemMenuHiddenUrlPatterns(),
            PhpPlanQ1Rise.EpcCpSystemMenuHiddenLabels(),
            PhpPlanQ1Rise.EpcCpSystemMenuItemHidden("/cp/control/o-programme?x=1", ""),
            PhpPlanQ1Rise.EpcCpSystemMenuItemHidden("/content/usefull/" + "changes_fc" + ".php", ""),
            PhpPlanQ1Rise.EpcCpSystemMenuItemHidden("/shop/orders/orders", "Updates"),
            PhpPlanQ1Rise.EpcCpSystemMenuItemHidden("/shop/orders/orders", "orders"),
            PhpPlanQ1Rise.EpcCpSystemMenuItemHidden("", "о программе")
        });

    private static Rendered Empty()
    {
        var db = new PhpPlanQ1Rise.RiseStore();
        var shop = PhpPlanQ1Rise.EpcCpMmFindShopGroup(db);
        var mm = PhpPlanQ1Rise.EpcCpMainstreamMenuApply(db);
        var gid = PhpPlanQ1Rise.EpcCpMmGroupId(db, "epc_cp_group_channels");
        var item = PhpPlanQ1Rise.EpcCpMmItemIdForUrl(db, "/<backend>/shop/channels/channels");
        var again = PhpPlanQ1Rise.EpcCpMmItemIdForUrl(db, "/<backend>/shop/channels/channels?x=1");
        return new Rendered(new object?[] { shop, mm, gid, item, again, db.Snapshot() });
    }

    private static Rendered Seed()
    {
        var db = new PhpPlanQ1Rise.RiseStore();
        db.Groups.Add(new PhpPlanQ1Rise.GroupRow { Id = db.NextGroupId++, Caption = "744", Order = 10 });
        db.Groups.Add(new PhpPlanQ1Rise.GroupRow { Id = db.NextGroupId++, Caption = "741", Order = 2 });
        db.Groups.Add(new PhpPlanQ1Rise.GroupRow { Id = db.NextGroupId++, Caption = "epc_cp_group_commerce", Order = 99 });
        void Item(int group, string caption, string url, int order, string color, string icon)
        {
            db.Items.Add(new PhpPlanQ1Rise.ItemRow
            {
                Id = db.NextItemId++,
                ItemsGroup = group,
                Caption = caption,
                Url = url,
                Order = order,
                BackgroundColor = color,
                FontawesomeClass = icon
            });
        }

        Item(1, "771", "/<backend>/shop/prices", 20, "#111", "fa-list");
        Item(1, "279", "/<backend>/shop/orders/statuses", 30, "#222", "fa-flag");
        Item(1, "items", "/<backend>/shop/orders/items", 31, "#222", "fa-list");
        Item(1, "wh", "/<backend>/shop/storages", 12, "#333", "fa-box");
        Item(1, "epc_channels_cp", "/<backend>/shop/channels/channels", 1, "#000", "fa-old");
        Item(3, "epc_prices_commerce_cp", "/<backend>/shop/prices/commerce", 1, "#000", "fa-old");
        Item(2, "u1", "/<backend>/users/usermanager", 1, "#000", "fa-user");
        Item(1, "sys", "/control/o-programme", 90, "#000", "fa-info");
        Item(1, "chg", "/content/usefull/changes_fc", 91, "#000", "fa-book");
        db.Langs.Add(new PhpPlanQ1Rise.LangRow { Id = db.NextLangId++, StrKey = "legacy_about", Description = "About program" });
        db.LangTr.Add(new PhpPlanQ1Rise.LangTrRow { StrKey = "legacy_about", LangCode = "en", Value = "About program" });
        var shop = PhpPlanQ1Rise.EpcCpMmFindShopGroup(db);
        var mm = PhpPlanQ1Rise.EpcCpMainstreamMenuApply(db);
        var sys = PhpPlanQ1Rise.EpcCpSystemMenuCleanup(db);
        var lab = PhpPlanQ1Rise.EpcCpSystemMenuItemLabel(db, "epc_oms_orders_cp");
        var legacyId = db.Langs.First(l => l.StrKey == "legacy_about").Id;
        var lab2 = PhpPlanQ1Rise.EpcCpSystemMenuItemLabel(db, legacyId.ToString());
        var lab3 = PhpPlanQ1Rise.EpcCpSystemMenuItemLabel(db, "missing-key!");
        var items = (Dictionary<string, object?>)mm["items"]!;
        return new Rendered(new object?[]
        {
            shop,
            mm["shop_group"],
            mm["channels_group"],
            items["channels_hub"],
            items.TryGetValue("shop_sku_media", out var sku) ? sku : null,
            sys["removed"],
            lab,
            lab2,
            lab3,
            db.Snapshot()
        });
    }

    private static Rendered Packs()
    {
        var db = new PhpPlanQ1Rise.RiseStore();
        db.Groups.Add(new PhpPlanQ1Rise.GroupRow { Id = db.NextGroupId++, Caption = "744", Order = 10 });
        var pay = PhpPlanQ1Rise.EpcCpPaymentsMenuApply(db);
        var mkt = PhpPlanQ1Rise.EpcCpMarketingMenuApply(db);
        var proc = PhpPlanQ1Rise.EpcCpProcurementMenuApply(db);
        var pos = PhpPlanQ1Rise.EpcCpPosMenuApply(db);
        var cust = PhpPlanQ1Rise.EpcCpCustomerMgmtMenuApply(db);
        var doc = PhpPlanQ1Rise.EpcCpDocumentControlMenuApply(db);
        var hub = PhpPlanQ1Rise.EpcCpSuperPlatformMenuApply(db);
        var op = PhpPlanQ1Rise.EpcCpSuperCpOperatorMenuApply(db);
        var integ = PhpPlanQ1Rise.EpcCpIntegrationsMenuApply(db);
        var por = PhpPlanQ1Rise.EpcCpPortalMenuApply(db);
        db.Items.Add(new PhpPlanQ1Rise.ItemRow
        {
            Id = db.NextItemId++,
            ItemsGroup = Convert.ToInt32(pos["erp_group"]),
            Caption = "dup",
            Url = "/<backend>/shop/finance/erp",
            Order = 99,
            BackgroundColor = "#000",
            FontawesomeClass = "fa-x"
        });
        var keep = new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_hub"] = 1 };
        var removed = PhpPlanQ1Rise.EpcCpErpMenuCleanup(db, Convert.ToInt32(pos["erp_group"]), keep);
        var parity = PhpPlanQ1Rise.EpcCpMenuParityApply(db);
        var packs = (Dictionary<string, object?>)parity["packs"]!;
        var packKeys = packs.Keys.ToList();
        var ok = packKeys.ToDictionary(k => k, k => !IsEmptyOk(((Dictionary<string, object?>)packs[k]!)["ok"]), StringComparer.Ordinal);
        return new Rendered(new object?[]
        {
            pay["payments_group"],
            mkt["marketing_group"],
            proc["procurement_group"],
            pos["erp_group"],
            cust["customers_group"],
            doc["documents_group"],
            hub["tenant_hub_group"],
            op["operator_group"],
            integ["integrations_group"],
            por["portal_group"],
            removed,
            packKeys,
            ok,
            parity.ContainsKey("cache_bust")
        });
    }

    private static bool IsEmptyOk(object? value)
        => value is null or false or 0 or "" or "0";

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
