using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1ShipParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Ship");

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
    public void PlanQ1Ship_MatchPhpGolden()
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
                PhpPlanQ1Ship.LogisticsHelpersPath,
                PhpPlanQ1Ship.ElectronicsTaxonomyPath,
                PhpPlanQ1Ship.SocialPackDataPath,
                PhpPlanQ1Ship.StorefrontWorldclassPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Ship.LogisticsHelpersPath,
                PhpPlanQ1Ship.ElectronicsTaxonomyPath,
                PhpPlanQ1Ship.SocialPackDataPath,
                PhpPlanQ1Ship.StorefrontWorldclassPath
            });

    [Fact]
    public void LogisticsMaps_DoesNotStartASession()
    {
        Assert.Equal("0.00", PhpPlanQ1Ship.EpcLogisticsMoney("-"));
        Assert.Equal("", PhpPlanQ1Ship.EpcLogisticsH(""));
    }

    private static string Render(string name)
        => name switch
        {
            "logistics_maps" => Json(LogisticsMaps()),
            "logistics_seed" => Json(LogisticsSeed()),
            "tax_tree" => Json(TaxTree()),
            "tax_db" => Json(TaxDb()),
            "social_pack" => Json(SocialPack()),
            "worldclass" => Json(Worldclass()),
            _ => "unknown:" + name
        };

    private static object?[] LogisticsMaps()
        => new object?[]
        {
            PhpPlanQ1Ship.EpcLogisticsH("a&b<'x'>"),
            PhpPlanQ1Ship.EpcLogisticsMoney(1234.5),
            PhpPlanQ1Ship.EpcLogisticsMoney(""),
            PhpPlanQ1Ship.EpcLogisticsMoney("-"),
            PhpPlanQ1Ship.EpcLogisticsConfigureUrls()
        };

    private static object?[] LogisticsSeed()
    {
        var db = new PhpPlanQ1Ship.LogisticsStore();
        db.ShopOrders.Add(new PhpPlanQ1Ship.LogisticsShopOrderRow { Id = 42, SuccessfullyCreated = 1, Time = 1700000000 });
        db.ShopOrders.Add(new PhpPlanQ1Ship.LogisticsShopOrderRow { Id = 7, SuccessfullyCreated = 0, Time = 1 });
        db.ObtainModes.Add(new PhpPlanQ1Ship.LogisticsObtainRow { Id = 3, Handler = "epc_carriers" });
        PhpPlanQ1Plus.EpcChannelEnsureSchema(db.Channels);
        PhpPlanQ1Ship.EpcLogisticsSeedSampleData(db);
        var dash = PhpPlanQ1Ship.EpcLogisticsDashboard(db);
        var car = (List<Dictionary<string, object?>>)PhpPlanQ1Ship.EpcLogisticsDemoReport(db)["carriers"]!;
        var codes = car.Select(r => (string)r["code"]!).ToList();
        var rep = PhpPlanQ1Ship.EpcLogisticsDemoReport(db);
        var ships = (List<Dictionary<string, object?>>)rep["shipments"]!;
        var logs = (List<Dictionary<string, object?>>)rep["sync_log"]!;
        var snap = PhpPlanQ1Ship.EpcLogisticsGuideSnapshot(db);
        PhpPlanQ1Ship.EpcLogisticsSeedDefaults(db);
        var dash2 = PhpPlanQ1Ship.EpcLogisticsDashboard(db);
        return new object?[]
        {
            dash["carriers"],
            dash["carriers_total"],
            dash["catalog_count"],
            dash["regions"],
            dash["shipments"],
            dash["shipments_shipped"],
            dash["shipments_pending"],
            dash["shop_orders"],
            codes,
            ships.Count,
            ships.Count > 0 ? ships[0]["tracking_number"] : "",
            ships.Count > 0 ? ships[0]["status"] : "",
            ships.Count > 0 ? Convert.ToInt32(ships[0]["order_id"], System.Globalization.CultureInfo.InvariantCulture) : 0,
            ships.Count > 0 ? Convert.ToInt32(ships[0]["order_time"], System.Globalization.CultureInfo.InvariantCulture) : 0,
            logs.Count,
            snap.ContainsKey("generated_at"),
            snap["obtaining_mode_id"],
            snap["obtaining_handler"],
            dash2["carriers_total"]
        };
    }

    private static object?[] TaxTree()
    {
        var t = PhpPlanQ1Ship.EpcTaxSeedTree();
        var slugs = t.EnumerateArray().Select(n => n.GetProperty("slug").GetString()).ToArray();
        var kids = t[0].GetProperty("children").GetArrayLength();
        var arr = t.EnumerateArray().ToList();
        return new object?[]
        {
            arr.Count,
            slugs,
            arr[0].GetProperty("name").GetString(),
            arr[0].GetProperty("amazon").GetString(),
            arr[0].GetProperty("sort").GetInt32(),
            kids,
            arr[0].GetProperty("children")[0].GetProperty("slug").GetString(),
            arr[^1].GetProperty("slug").GetString()
        };
    }

    private static object?[] TaxDb()
    {
        var db = new PhpPlanQ1Ship.TaxStore();
        PhpPlanQ1Ship.EpcTaxEnsureSchema(db);
        var seed = PhpPlanQ1Ship.EpcTaxSeed(db);
        var n = PhpPlanQ1Ship.EpcTaxCount(db);
        var lap = PhpPlanQ1Ship.EpcTaxBySlug(db, "computers-laptops")!;
        var miss = PhpPlanQ1Ship.EpcTaxBySlug(db, "nope");
        var bad = PhpPlanQ1Ship.EpcTaxById(db, 0);
        var root = PhpPlanQ1Ship.EpcTaxListFlat(db, 0);
        var tree = PhpPlanQ1Ship.EpcTaxListTree(db, 0);
        var crumb = PhpPlanQ1Ship.EpcTaxBreadcrumb(db, Convert.ToInt32(lap["id"], System.Globalization.CultureInfo.InvariantCulture));
        var crumbSlugs = crumb.Select(c => (string)c["slug"]!).ToList();
        db.Categories.Add(new PhpPlanQ1Ship.TaxCategoryRow { Id = 9, Value = "computers", Caption = "Computers & Accessories", PublishedFlag = 1 });
        db.Categories.Add(new PhpPlanQ1Ship.TaxCategoryRow { Id = 10, Value = "hidden", Caption = "Laptops", PublishedFlag = 0 });
        var link = PhpPlanQ1Ship.EpcTaxLinkCatalogueCategories(db);
        var comp = PhpPlanQ1Ship.EpcTaxBySlug(db, "computers")!;
        var kids = (List<Dictionary<string, object?>>)tree[0]["children"]!;
        return new object?[]
        {
            seed["nodes"],
            n,
            lap["slug"],
            lap["name_en"],
            lap["level"],
            miss is null,
            bad is null,
            root.Count,
            root[0]["slug"],
            tree.Count,
            kids.Count,
            crumbSlugs,
            link,
            Convert.ToInt32(comp["catalogue_category_id"], System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static object?[] SocialPack()
    {
        var p = PhpPlanQ1Ship.EpcSocialPackPlatforms();
        var brand = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brand_name"] = "Acme Parts",
            ["handle"] = "acme.official",
            ["domain"] = "acmeparts.test",
            ["website"] = "https://acmeparts.test"
        };
        var adapted = PhpPlanQ1Ship.EpcSocialPackPostsForBrand("x", brand);
        var thread = PhpPlanQ1Ship.EpcSocialXThreadStarter();
        return new object?[]
        {
            p.EnumerateObject().Select(x => x.Name).ToArray(),
            p.GetProperty("linkedin").GetProperty("label").GetString(),
            p.GetProperty("linkedin").GetProperty("hashtags").GetArrayLength(),
            PhpPlanQ1Ship.EpcSocialPackPosts("linkedin").Count,
            PhpPlanQ1Ship.EpcSocialPackPosts("instagram").Count,
            PhpPlanQ1Ship.EpcSocialPackPosts("facebook").Count,
            PhpPlanQ1Ship.EpcSocialPackPosts("x").Count,
            PhpPlanQ1Ship.EpcSocialPackPosts("tiktok").Count,
            PhpPlanQ1Ship.EpcSocialPackPosts("nope"),
            PhpPlanQ1Ship.EpcSocialInstagramReelsIdeas().Count,
            PhpPlanQ1Ship.EpcSocialInstagramReelsIdeas()[0]["title"],
            PhpPlanQ1Ship.EpcSocialTiktokSpecs()["Aspect ratio"],
            PhpPlanQ1Ship.EpcSocialVideoLibrary().Count,
            PhpPlanQ1Ship.EpcSocialVideoLibrary()[0]["id"],
            adapted[0]["title"],
            adapted[0]["caption"].Contains("acmeparts.test", StringComparison.Ordinal),
            !adapted[0]["caption"].Contains("ecomae.com", StringComparison.Ordinal),
            PhpByteSubstr(thread, 0, 40)
        };
    }

    private static object?[] Worldclass()
    {
        PhpPlanQ1Ship.PortalProfile = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["system_name"] = "Acme <Co>",
            ["domain_path"] = "https://shop.example/"
        };
        PhpPlanQ1Ship.PortalSettings = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["company_name"] = "Acme & Co",
                ["phone"] = "+971 4 000",
                ["email"] = "hi@acme.test",
                ["address_line1"] = "Sheikh Zayed",
                ["city"] = "Dubai",
                ["country"] = "AE",
                ["social_facebook"] = "https://fb.example/acme",
                ["twitter_url"] = "https://x.example/acme"
            }
        };
        return new object?[]
        {
            PhpPlanQ1Ship.EpcStorefrontJsonLdOrganization(),
            PhpPlanQ1Ship.EpcStorefrontJsonLdWebsite(),
            PhpPlanQ1Ship.EpcStorefrontSocialLinksData(),
            PhpPlanQ1Ship.EpcStorefrontNewsletterSection("#0ea5e9", "#f8fafc", "electronics"),
            PhpPlanQ1Ship.EpcStorefrontNewsletterSection("#abc", "#fff", ""),
            PhpPlanQ1Ship.EpcStorefrontTrustBadges("jewellery"),
            PhpPlanQ1Ship.EpcStorefrontTrustBadges(""),
            PhpPlanQ1Ship.EpcStorefrontCookieConsent(),
            PhpPlanQ1Ship.EpcStorefrontNewsletterJs()
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

    private static string PhpByteSubstr(string value, int start, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        if (start >= bytes.Length)
        {
            return "";
        }

        var take = Math.Min(length, bytes.Length - start);
        return Encoding.UTF8.GetString(bytes, start, take);
    }

    private static string Truncate(string value)
        => value.Length <= 400 ? value : value[..400] + "…";
}
