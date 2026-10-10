using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1MoreParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1More");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpPlanQ1More.IndustryCatalogPath,
        PhpPlanQ1More.StorefrontLayoutsPath,
        PhpPlanQ1More.IndustrySubdomainRouterPath,
        PhpPlanQ1More.DealerPortalPath,
        PhpPlanQ1More.SocialHubCssPath
    };

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
    public void PlanQ1More_MatchPhpGolden()
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
        => Assert.Equal(CoveredFiles, new HashSet<string>(StringComparer.Ordinal)
        {
            CaseFile("cat_profile"),
            CaseFile("lay_reg"),
            CaseFile("sub_resolve"),
            CaseFile("dealer_flow"),
            CaseFile("social_css")
        });

    [Fact]
    public void CatalogProfile_DoesNotStartASession()
        => Assert.Null(PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "auto_parts" }, "", true));

    private static string CaseFile(string name)
        => name.StartsWith("cat_", StringComparison.Ordinal) ? PhpPlanQ1More.IndustryCatalogPath
            : name.StartsWith("lay_", StringComparison.Ordinal) ? PhpPlanQ1More.StorefrontLayoutsPath
            : name.StartsWith("sub_", StringComparison.Ordinal) ? PhpPlanQ1More.IndustrySubdomainRouterPath
            : name.StartsWith("dealer_", StringComparison.Ordinal) ? PhpPlanQ1More.DealerPortalPath
            : name == "social_css" ? PhpPlanQ1More.SocialHubCssPath
            : "unknown";

    private static string Render(string name)
        => name switch
        {
            "cat_noprofile" => Json(PhpPlanQ1More.EpcPortalIndustryCatalogProfile(null, null, false)),
            "cat_profile" => Json(new object?[]
            {
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "auto_parts" }, ""),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "auto_parts" }, "automotive_spareparts_pro"),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "electronics" }, ""),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "auto_parts" }, "electronics_retail_virgin"),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "tax_advisory" }, ""),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "consultancy" }, "consulting_primeinvest"),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "fashion" }, ""),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "jewellery" }, ""),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?> { ["industry"] = "printing_signage" }, ""),
                PhpPlanQ1More.EpcPortalIndustryCatalogProfile(new Dictionary<string, object?>(), "")
            }),
            "cat_title" => Json(new[]
            {
                PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle("electronics", id => "T" + id),
                PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle("consulting", id => "T" + id),
                PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle("fashion", id => "T" + id),
                PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle("jewellery", id => "T" + id),
                PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle("other", id => "T" + id),
                PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle(null, id => "T" + id)
            }),
            "cat_title_plain" => Json(PhpPlanQ1More.EpcPortalIndustryCatalogSectionTitle("x")),
            "cat_unsplash" => Json(new[]
            {
                PhpPlanQ1More.EpcPortalIndustryCatalogUnsplash("photo-1450101499163-c8848c66ca85"),
                PhpPlanQ1More.EpcPortalIndustryCatalogUnsplash("photo-local", 200, 80, rel => rel.EndsWith("/photo-local.jpg", StringComparison.Ordinal)),
                PhpPlanQ1More.EpcPortalIndustryCatalogUnsplash("photo-x", "400.9", "1e3")
            }),
            "cat_cats" => Json(CatCats()),
            "cat_render" => Json(new object?[]
            {
                PhpPlanQ1More.EpcPortalIndustryCatalogRender("fashion", "/en"),
                PhpPlanQ1More.EpcPortalIndustryCatalogRender("auto"),
                null
            }),
            "lay_reg" => Json(LayReg()),
            "lay_for" => Json(LayFor()),
            "lay_active" => Json(new[]
            {
                PhpPlanQ1More.EpcStorefrontActiveLayout(new Dictionary<string, object?> { ["storefront_layout"] = "trend_feed" }),
                PhpPlanQ1More.EpcStorefrontActiveLayout(new Dictionary<string, object?> { ["storefront_layout"] = "", ["industry_code"] = "jewellery" }),
                PhpPlanQ1More.EpcStorefrontActiveLayout(new Dictionary<string, object?>()),
                PhpPlanQ1More.EpcStorefrontActiveLayout(new Dictionary<string, object?> { ["storefront_layout"] = "  brand_focused  " })
            }),
            "lay_active_portal" => Json(PhpPlanQ1More.EpcStorefrontActiveLayout(null, () => new Dictionary<string, object?> { ["storefront_layout"] = "", ["industry_code"] = "electronics" })),
            "lay_js" => Json(LayJs()),
            "sub_detect_auto" => Json(new object?[] { PhpPlanQ1More.EpcIndustrySubdomainDetect("automotive.ecomae.com"), PhpPlanQ1More.EpcIsIndustrySubdomain("automotive.ecomae.com") }),
            "sub_detect_www" => Json(new object?[] { PhpPlanQ1More.EpcIndustrySubdomainDetect("www.ecomae.com"), PhpPlanQ1More.EpcIsIndustrySubdomain("www.ecomae.com") }),
            "sub_detect_port" => Json(PhpPlanQ1More.EpcIndustrySubdomainDetect("Printing.ecomae.com:8080")),
            "sub_detect_empty" => Json(PhpPlanQ1More.EpcIndustrySubdomainDetect(null, "homeliving.ecomae.com")),
            "sub_detect_other" => Json(PhpPlanQ1More.EpcIndustrySubdomainDetect("shop.example.com")),
            "sub_resolve" => Json(new[]
            {
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("construction"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("medicalequipment"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("auto"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("foodbeverage"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("furniture"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("unknownxyz"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("retailhub"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("cctv"),
                PhpPlanQ1More.EpcIndustrySubdomainResolveGroup("professional")
            }),
            "sub_boot" => Json(SubBoot()),
            "sub_boot_none" => Json(SubBootNone()),
            "dealer_tiers" => Json(new object[] { PhpPlanQ1More.DealerPortalVersion, PhpPlanQ1More.EpcDealerTierDiscounts() }),
            "dealer_flow" => Json(DealerFlow()),
            "social_css" => Json(new object[]
            {
                System.Text.Encoding.UTF8.GetByteCount(PhpPlanQ1More.EpcSocialMediaHubCss()),
                PhpPlanQ1More.EpcSocialMediaHubCss()[..48],
                PhpPlanQ1More.EpcSocialMediaHubCss()[^32..],
                PhpPlanQ1More.EpcSocialMediaHubCssHeaders()
            }),
            _ => "unknown:" + name
        };

    private static object?[] CatCats()
    {
        var el = PhpPlanQ1More.EpcPortalIndustryCatalogCategories("electronics", "/ar");
        var co = PhpPlanQ1More.EpcPortalIndustryCatalogCategories("consulting", "/ar");
        var fa = PhpPlanQ1More.EpcPortalIndustryCatalogCategories("fashion", "/ar");
        var je = PhpPlanQ1More.EpcPortalIndustryCatalogCategories("jewellery", "/ar");
        var df = PhpPlanQ1More.EpcPortalIndustryCatalogCategories("auto", "/ar");
        return new object?[] { el.Count, el[0], el[5]["href"], co.Count, co[0]["href"], co[4]["label"], fa.Count, fa[0]["alt"], je.Count, je[2]["label"], df };
    }

    private static object?[] LayReg()
    {
        var r = PhpPlanQ1More.EpcStorefrontLayoutRegistry();
        return new object?[]
        {
            r.EnumerateObject().Select(p => p.Name).ToArray(),
            r.GetProperty("electronics").GetArrayLength(),
            r.GetProperty("jewellery")[0].GetProperty("id").GetString(),
            r.GetProperty("jewellery")[0].GetProperty("default").GetBoolean(),
            r.GetProperty("fashion")[1].TryGetProperty("default", out _),
            r.GetProperty("tax_advisory")[0].GetProperty("sections").EnumerateArray().Select(s => s.GetString()).ToArray()
        };
    }

    private static object?[] LayFor()
    {
        var fashion = PhpPlanQ1More.EpcStorefrontLayoutsForIndustry("fashion");
        var missing = PhpPlanQ1More.EpcStorefrontLayoutsForIndustry("nope");
        var meta = PhpPlanQ1More.EpcStorefrontLayoutMeta("electronics", "product_showcase");
        return new object?[]
        {
            fashion.EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray(),
            JsonSerializer.Deserialize<object>(missing.GetRawText()),
            PhpPlanQ1More.EpcStorefrontLayoutDefault("electronics"),
            PhpPlanQ1More.EpcStorefrontLayoutDefault("fashion"),
            PhpPlanQ1More.EpcStorefrontLayoutDefault("jewellery"),
            PhpPlanQ1More.EpcStorefrontLayoutDefault("tax_advisory"),
            PhpPlanQ1More.EpcStorefrontLayoutDefault("consultancy"),
            PhpPlanQ1More.EpcStorefrontLayoutDefault("missing"),
            meta?["label"] is JsonElement el ? el.GetString() : meta?["label"],
            PhpPlanQ1More.EpcStorefrontLayoutMeta("electronics", "nope"),
            PhpPlanQ1More.EpcStorefrontLayoutSections("fashion", "editorial"),
            PhpPlanQ1More.EpcStorefrontLayoutSections("fashion", "nope")
        };
    }

    private static object?[] LayJs()
    {
        var j = PhpPlanQ1More.EpcStorefrontLayoutsForJs();
        return new object?[]
        {
            j.Keys.ToArray(),
            j["electronics"][0],
            j["electronics"][0].ContainsKey("preview"),
            j["consultancy"][0]["default"]
        };
    }

    private static object?[] SubBoot()
    {
        var cfg = new Dictionary<string, object?> { ["domain_path"] = "https://old.example/" };
        var r = PhpPlanQ1More.EpcIndustrySubdomainBootstrap("Automotive.ecomae.com:443", cfg);
        return new object?[] { r["ok"], r["active"], r["slug"], r["group"], r["domain_path"], r["epc_portal_industry"] };
    }

    private static object?[] SubBootNone()
    {
        var cfg = new Dictionary<string, object?> { ["domain_path"] = "https://old/" };
        var r = PhpPlanQ1More.EpcIndustrySubdomainBootstrap("www.ecomae.com", cfg);
        return new object?[] { r["ok"], cfg["domain_path"], true };
    }

    private static object?[] DealerFlow()
    {
        var store = new PhpPlanQ1More.DealerStore();
        PhpPlanQ1More.EpcDealerEnsureSchema();
        var a = PhpPlanQ1More.EpcDealerRegister(store, "siteA", new Dictionary<string, object?>
        {
            ["dealer_code"] = "dlr-one",
            ["company_name"] = "Acme",
            ["contact_name"] = "Pat",
            ["email"] = "a@x",
            ["phone"] = "1",
            ["tier"] = "gold",
            ["credit_limit"] = 1000,
            ["territory"] = "DXB"
        });
        var b = PhpPlanQ1More.EpcDealerRegister(store, "siteB", new Dictionary<string, object?>
        {
            ["dealer_code"] = "DLR-TWO",
            ["company_name"] = "Beta",
            ["tier"] = "bronze"
        });
        var list = PhpPlanQ1More.EpcDealerList(store, "siteA");
        var listName = list[0].CompanyName;
        var listDiscount = list[0].DiscountPct;
        var got = PhpPlanQ1More.EpcDealerGet(store, Convert.ToInt32(a["dealer_id"]));
        var miss = PhpPlanQ1More.EpcDealerPlaceOrder(store, "siteA", Convert.ToInt32(a["dealer_id"]), 1000);
        var act = PhpPlanQ1More.EpcDealerActivate(store, Convert.ToInt32(a["dealer_id"]));
        var ord = PhpPlanQ1More.EpcDealerPlaceOrder(store, "siteA", Convert.ToInt32(a["dealer_id"]), 1000, now: new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc));
        var tier = PhpPlanQ1More.EpcDealerAutoTier(store, Convert.ToInt32(a["dealer_id"]));
        store.Dealers.First(d => d.Id == Convert.ToInt32(a["dealer_id"])).YtdRevenue = 200000;
        var tier2 = PhpPlanQ1More.EpcDealerAutoTier(store, Convert.ToInt32(a["dealer_id"]));
        var upd = PhpPlanQ1More.EpcDealerUpdate(store, Convert.ToInt32(a["dealer_id"]), new Dictionary<string, object?> { ["company_name"] = "Acme LLC", ["phone"] = "99" });
        var emptyUpd = PhpPlanQ1More.EpcDealerUpdate(store, Convert.ToInt32(a["dealer_id"]), new Dictionary<string, object?> { ["nope"] = 1 });
        var sus = PhpPlanQ1More.EpcDealerSuspend(store, Convert.ToInt32(a["dealer_id"]), "x");
        var rep = PhpPlanQ1More.EpcDealerPerformanceReport(store, "siteA");
        var fleet = PhpPlanQ1More.EpcDealerFleetStats(store);
        var filt = PhpPlanQ1More.EpcDealerList(store, "siteA", new Dictionary<string, object?> { ["tier"] = "gold", ["status"] = "suspended" });
        var got2 = PhpPlanQ1More.EpcDealerGet(store, Convert.ToInt32(a["dealer_id"]));
        return new object?[]
        {
            a["ok"], a["dealer_code"], b["dealer_code"], list.Count, listName, listDiscount, got!["tier"], miss, act,
            ord["ok"], ord["discount"], ord["net_total"], ((string)ord["order_ref"]!)[..12], tier, tier2["new_tier"], tier2["old_tier"],
            upd, emptyUpd, sus, rep, fleet[0]["site_key"], fleet[0]["total_dealers"], filt.Count, got2!["company_name"], got2["status"]
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
        => value.Length <= 280 ? value : value[..280] + "…";
}
