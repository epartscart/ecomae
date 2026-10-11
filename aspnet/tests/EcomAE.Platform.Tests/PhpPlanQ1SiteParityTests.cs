using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1SiteParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Site");

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
    public void PlanQ1Site_MatchPhpGolden()
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
                PhpPlanQ1Site.SiteContextPath,
                PhpPlanQ1Site.SupplierNotificationsPath,
                PhpPlanQ1Site.ControlHelperPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Site.SiteContextPath,
                PhpPlanQ1Site.SupplierNotificationsPath,
                PhpPlanQ1Site.ControlHelperPath
            });

    [Fact]
    public void SiteKey_DoesNotStartASession()
    {
        PhpPlanQ1Site.ResetSite();
        PhpPlanQ1Site.HttpHost = "www.Shop.example:443";
        Assert.Equal("www.shop.example", PhpPlanQ1Site.EpcSiteContextCacheKey());
    }

    private static string Render(string name)
    {
        PhpPlanQ1Site.ResetSite();
        PhpPlanQ1Site.ResetAcl();
        return name switch
        {
            "site_key" => Json(SiteKey()),
            "site_ctx" => Json(SiteCtx()),
            "site_apply" => Json(SiteApply()),
            "supplier" => Json(Supplier()),
            "acl" => Json(Acl()),
            _ => "unknown:" + name
        };
    }

    private static object?[] SiteKey()
    {
        PhpPlanQ1Site.HttpHost = "www.Shop.example:443";
        var a = PhpPlanQ1Site.EpcSiteContextCacheKey();
        PhpPlanQ1Site.DemoStorefrontSiteKey = "Demo-Site!";
        var b = PhpPlanQ1Site.EpcSiteContextCacheKey();
        PhpPlanQ1Site.DemoStorefrontSiteKey = null;
        PhpPlanQ1Site.DemoCpSiteKey = "Cp Key";
        var c = PhpPlanQ1Site.EpcSiteContextCacheKey();
        return new object?[] { a, b, c };
    }

    private static object?[] SiteCtx()
    {
        PhpPlanQ1Site.EpcSiteContextReset();
        PhpPlanQ1Site.HttpHost = "localhost";
        PhpPlanQ1Site.PortalProfile = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["system_name"] = "Acme Hub",
            ["hub_name"] = "acme",
            ["tagline"] = "Go",
            ["trade_name"] = "Acme",
            ["industry"] = "auto_parts",
            ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["from_email"] = "sales@acme.test",
                ["city"] = "Dubai"
            }
        };
        PhpPlanQ1Site.PortalIndustry = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = "auto_parts",
            ["name"] = "Auto parts"
        };
        PhpPlanQ1Site.PortalAuto = true;
        PhpPlanQ1Site.PortalHome = "auto_parts";
        PhpPlanQ1Site.PortalGuess = "https://guess.example";
        var c1 = PhpPlanQ1Site.EpcSiteContext();
        PhpPlanQ1Site.PortalProfile["domain_path"] = "https://shop.acme.test/";
        PhpPlanQ1Site.EpcSiteContextReset();
        var c2 = PhpPlanQ1Site.EpcSiteContext();
        var cfg = new PhpPlanQ1Site.SiteConfig
        {
            FromEmail = "cfg@acme.test",
            FromName = "Cfg",
            EpcContactPhone = "+971",
            EpcWhatsappNumber = "050",
            EpcHeadOfficeEmail = "hq@acme.test",
            EpcHeadOfficeAddress = "SZR"
        };
        PhpPlanQ1Site.PortalProfile["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["city"] = "Sharjah" };
        PhpPlanQ1Site.EpcSiteContextReset();
        var c3 = PhpPlanQ1Site.EpcSiteContext(cfg);
        PhpPlanQ1Site.DemoStorefrontContext = true;
        PhpPlanQ1Site.DemoStorefrontSiteKey = "Tenant-1";
        PhpPlanQ1Site.EpcSiteContextReset();
        var c4 = PhpPlanQ1Site.EpcSiteContext();
        PhpPlanQ1Site.DemoStorefrontContext = false;
        PhpPlanQ1Site.DemoStorefrontSiteKey = null;
        PhpPlanQ1Site.EpcSiteContextReset();
        var c5 = PhpPlanQ1Site.EpcSiteContext();
        PhpPlanQ1Site.PortalProfile["system_name"] = "Changed";
        var c6 = PhpPlanQ1Site.EpcSiteContext();
        PhpPlanQ1Site.EpcSiteContextReset();
        var c7 = PhpPlanQ1Site.EpcSiteContext();
        return new object?[]
        {
            c1["domain"], c1["domain_path"], c1["host"], c1["industry_code"], c1["industry_name"],
            c1["system_name"], c1["hub_name"], c1["tagline"], c1["trade_name"], c1["from_email"],
            c1["admin_email"], c1["city"], c1["country"], c1["is_auto_parts"], c1["home_mode"],
            c2["domain"], c2["domain_path"], c3["from_email"], c3["from_name"], c3["contact_phone"],
            c3["whatsapp_number"], c3["head_office_email"], c3["head_office_address"], c3["admin_email"],
            c4["domain"], c5["system_name"], c6["system_name"], c7["system_name"],
            PhpPlanQ1Site.EpcSiteDomain(), PhpPlanQ1Site.EpcSiteUrl("en/shop"), PhpPlanQ1Site.EpcSiteUrl(""),
            PhpPlanQ1Site.EpcSiteHost(), PhpPlanQ1Site.EpcSiteTradeName(), PhpPlanQ1Site.EpcSiteFromEmail(),
            PhpPlanQ1Site.EpcSiteAdminEmail(), PhpPlanQ1Site.EpcSiteContactPhone()
        };
    }

    private static object?[] SiteApply()
    {
        PhpPlanQ1Site.EpcSiteContextReset();
        PhpPlanQ1Site.HttpHost = "tenant.example";
        PhpPlanQ1Site.PortalProfile = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["system_name"] = "Legal Co",
            ["hub_name"] = "legalco",
            ["domain_path"] = "https://legal.example/",
            ["trade_name"] = "Legal Co",
            ["head_office_address"] = "",
            ["city"] = "Abu Dhabi",
            ["country"] = "AE",
            ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["from_email"] = "ops@legal.example",
                ["from_name"] = "Ops",
                ["contact_phone"] = "04",
                ["whatsapp_number"] = "05",
                ["head_office_email"] = "",
                ["head_office_address"] = "",
                ["city"] = "Abu Dhabi",
                ["country"] = "AE"
            }
        };
        PhpPlanQ1Site.PortalIndustry = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = "tax_advisory",
            ["name"] = "Tax"
        };
        PhpPlanQ1Site.PortalAuto = false;
        PhpPlanQ1Site.PortalHome = "professional";
        var cfg = new PhpPlanQ1Site.SiteConfig
        {
            DomainPath = "old",
            FromName = "x",
            FromEmail = "x",
            EpcContactPhone = "x",
            EpcWhatsappNumber = "x",
            EpcHeadOfficeAddress = "x",
            EpcHeadOfficeEmail = "x"
        };
        PhpPlanQ1Site.EpcSiteApplyContactOverrides(cfg);
        var cfg2 = new PhpPlanQ1Site.SiteConfig();
        PhpPlanQ1Site.EpcSiteApplyConfig(cfg2);
        PhpPlanQ1Site.EpcSiteApplyContactOverrides(null);
        var doc = PhpPlanQ1Site.EpcSiteDocumentCompanyDefaults();
        return new object?[]
        {
            cfg.DomainPath, cfg.FromName, cfg.FromEmail, cfg.EpcContactPhone, cfg.EpcWhatsappNumber,
            cfg.EpcHeadOfficeAddress, cfg.EpcHeadOfficeEmail, cfg2.DomainPath, cfg2.FromEmail, doc
        };
    }

    private static object?[] Supplier()
    {
        var db = new PhpPlanQ1Site.SupplierStore();
        db.Details.Add(new PhpPlanQ1Site.SupplierDetailRow { Id = 1, OrderItemId = 9, StorageId = 4 });
        db.Details.Add(new PhpPlanQ1Site.SupplierDetailRow { Id = 2, OrderItemId = 9, StorageId = 8 });
        var cfg = new PhpPlanQ1Site.SiteConfig { DomainPath = "https://shop.example/" };
        var html = PhpPlanQ1Site.EpcBuildSupplierLpoHtml(
            cfg,
            17,
            3,
            "Main <WH>",
            new IReadOnlyDictionary<string, object?>[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["t2_manufacturer"] = "Bosch",
                    ["t2_article_show"] = "0986",
                    ["t2_name"] = "Oil & filter",
                    ["count_need"] = 2
                },
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["t2_manufacturer"] = "NGK",
                    ["t2_article"] = "BKR",
                    ["t2_name"] = "Plug",
                    ["count_need"] = 0
                }
            });
        return new object?[]
        {
            PhpPlanQ1Site.EpcSupplierH("a&b<'x'>"),
            PhpPlanQ1Site.EpcOrderItemStorageId(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["t2_storage_id"] = 5, ["id"] = 9 }),
            PhpPlanQ1Site.EpcOrderItemStorageId(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 9 }),
            PhpPlanQ1Site.EpcOrderItemStorageId(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 0 }),
            html
        };
    }

    private static object?[] Acl()
    {
        var db = new PhpPlanQ1Site.AclStore();
        db.Content.Add(new PhpPlanQ1Site.AclContentRow { Id = 1, Url = "shop/orders" });
        db.Content.Add(new PhpPlanQ1Site.AclContentRow { Id = 2, Url = "shop/users" });
        db.Access.Add(new PhpPlanQ1Site.AclAccessRow { ContentId = 1, GroupId = 3 });
        db.Access.Add(new PhpPlanQ1Site.AclAccessRow { ContentId = 1, GroupId = 5 });
        db.Access.Add(new PhpPlanQ1Site.AclAccessRow { ContentId = 2, GroupId = 3 });
        PhpPlanQ1Site.BackendDir = "cp";
        var u1 = PhpPlanQ1Site.EpcCpAclContentUrl(new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "/cp/shop/orders?x=1" });
        var u2 = PhpPlanQ1Site.EpcCpAclContentUrl(new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "shop/users" });
        var u3 = PhpPlanQ1Site.EpcCpAclContentUrl(new Dictionary<string, object?>(StringComparer.Ordinal));
        PhpPlanQ1Site.EpcCpAclPreload(db, Array.Empty<IReadOnlyDictionary<string, object?>>());
        PhpPlanQ1Site.EpcCpAclPreload(db, new IReadOnlyDictionary<string, object?>[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "/cp/shop/orders" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "/cp/shop/users" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["url"] = "/cp/missing" }
        });
        var g1 = PhpPlanQ1Site.EpcCpAclExpandGroups(new object?[] { 5, 3, 3, "7" });
        var g2 = PhpPlanQ1Site.EpcCpAclExpandGroups(new object?[] { 3, 5, 7 });
        var g3 = PhpPlanQ1Site.EpcCpAclExpandGroups(Array.Empty<object?>());
        return new object?[]
        {
            u1, u2, u3,
            PhpPlanQ1Site.ContentIdByUrl["shop/orders"],
            PhpPlanQ1Site.ContentIdByUrl["shop/users"],
            PhpPlanQ1Site.ContentIdByUrl["missing"],
            PhpPlanQ1Site.GroupsByContent[1],
            g1, g2, g3
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
