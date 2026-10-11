using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1KeepParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Keep");

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
    public void PlanQ1Keep_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Keep.IndustrySeoPath, PhpPlanQ1Keep.BocScopePath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Keep.IndustrySeoPath, PhpPlanQ1Keep.BocScopePath });

    [Fact]
    public void HostMap_DoesNotStartASession()
    {
        PhpPlanQ1Keep.Reset();
        Assert.Equal("technology", PhpPlanQ1Keep.EpcIndustrySeoHostMap()["it_software"]);
        Assert.Null(PhpPlanQ1Keep.EpcBocActiveTenant());
    }

    private static string Render(string name)
    {
        PhpPlanQ1Keep.Reset();
        return name switch
        {
            "seo_pure" => Json(SeoPure()),
            "seo_parse" => Json(SeoParse()),
            "boc_pure" => Json(BocPure()),
            "boc_html" => Json(BocHtml()),
            _ => "unknown:" + name
        };
    }

    private static object? SeoPure()
    {
        PhpPlanQ1Keep.Server["HTTP_HOST"] = "Energy.ecomae.com:443";
        PhpPlanQ1Keep.Server["HTTPS"] = "on";
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/biomass?x=1";
        var slugs = new object?[]
        {
            PhpPlanQ1Keep.EpcIndustrySeoSubSlug("Biomass & bioenergy"),
            PhpPlanQ1Keep.EpcIndustrySeoSubSlug("  Foo++Bar  "),
            PhpPlanQ1Keep.EpcIndustrySeoSubSlug("!!!"),
            PhpPlanQ1Keep.EpcIndustrySeoSubSlug(""),
            PhpPlanQ1Keep.EpcIndustrySeoSubSlug("Auto-Price")
        };
        var pres = new object?[]
        {
            PhpPlanQ1Keep.EpcIndustrySeoSubPresentation("biomass-bioenergy"),
            PhpPlanQ1Keep.EpcIndustrySeoSubPresentation("keep-alpha"),
            PhpPlanQ1Keep.EpcIndustrySeoSubPresentation("zero"),
            PhpPlanQ1Keep.EpcIndustrySeoSubPresentation("dock")
        };
        var map = PhpPlanQ1Keep.EpcIndustrySeoHostMap();
        var hosts = new object?[]
        {
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("energy"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("realestate"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("consulting"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("food"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("technology"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("homeliving"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("!!!"),
            PhpPlanQ1Keep.EpcIndustrySeoPrimaryHost("")
        };
        var urls = new object?[]
        {
            PhpPlanQ1Keep.EpcIndustrySeoSiteUrlForTemplate("automotive"),
            PhpPlanQ1Keep.EpcIndustrySeoSiteUrlForTemplate("it_software"),
            PhpPlanQ1Keep.EpcIndustrySeoSiteUrlForTemplate("unknown_key"),
            PhpPlanQ1Keep.EpcIndustrySeoSiteUrlForTemplate("")
        };
        var subs = new[] { "Biomass & bioenergy", "Keep Alpha", "Zero" };
        var m1 = PhpPlanQ1Keep.EpcIndustrySeoMatchRequestSub(subs);
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/keep-alpha";
        var m2 = PhpPlanQ1Keep.EpcIndustrySeoMatchRequestSub(subs);
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/cp/control";
        var m3 = PhpPlanQ1Keep.EpcIndustrySeoMatchRequestSub(subs);
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/";
        var m4 = PhpPlanQ1Keep.EpcIndustrySeoMatchRequestSub(subs);
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/bioenergy";
        var m5 = PhpPlanQ1Keep.EpcIndustrySeoMatchRequestSub(subs);
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/abc";
        var m6 = PhpPlanQ1Keep.EpcIndustrySeoMatchRequestSub(subs);
        var host = PhpPlanQ1Keep.EpcIndustrySeoRequestHost();
        var flags = new object?[]
        {
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("energy.ecomae.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("www.ecomae.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("ecomae.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("cp.ecomae.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("mail.ecomae.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("foo.bar.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("ENERGY.ecomae.com"),
            PhpPlanQ1Keep.EpcIndustrySeoIsIndustryHost("")
        };
        var b1 = PhpPlanQ1Keep.EpcIndustrySeoRequestHostBase();
        PhpPlanQ1Keep.Server["HTTPS"] = "0";
        PhpPlanQ1Keep.Server["HTTP_X_FORWARDED_PROTO"] = "";
        PhpPlanQ1Keep.Server["SERVER_PORT"] = "80";
        PhpPlanQ1Keep.Server["HTTP_HOST"] = "jewellery.ecomae.com";
        var b2 = PhpPlanQ1Keep.EpcIndustrySeoRequestHostBase();
        PhpPlanQ1Keep.Server["HTTPS"] = "off";
        PhpPlanQ1Keep.Server["HTTP_X_FORWARDED_PROTO"] = "https";
        var b3 = PhpPlanQ1Keep.EpcIndustrySeoRequestHostBase();
        PhpPlanQ1Keep.Server["HTTPS"] = "";
        PhpPlanQ1Keep.Server["HTTP_X_FORWARDED_PROTO"] = "";
        PhpPlanQ1Keep.Server["SERVER_PORT"] = "443";
        var b4 = PhpPlanQ1Keep.EpcIndustrySeoRequestHostBase();
        PhpPlanQ1Keep.Server["HTTP_HOST"] = "www.ecomae.com";
        var b5 = PhpPlanQ1Keep.EpcIndustrySeoRequestHostBase();
        return new object?[] { slugs, pres, map.Count, map["it_software"], map["food_beverage"], hosts, urls, m1, m2, m3, m4, m5, m6, host, flags, b1, b2, b3, b4, b5 };
    }

    private static object? SeoParse()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1k_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "general_pages", "industry_templates"));
        File.WriteAllText(
            Path.Combine(doc, "content", "general_pages", "industry_templates", "keeptest.php"),
            """
            <?php
            $industryData = array(
            	'sub_industries' => array('Keep Alpha', 'Keep Beta & bio', 'Zero'),
            	'Keep Alpha' => array(
            		'note' => 'x',
            		'categories' => array('Cat A', 'Cat B'),
            	),
            	'Keep Beta & bio' => array(
            		'categories' => array('X', 'Y\'s'),
            	),
            );
            """);
        PhpPlanQ1Keep.DocumentRoot = doc;
        PhpPlanQ1Keep.LiveDefs = () =>
        [
            new(StringComparer.Ordinal) { ["template_key"] = "keeptest", ["mode"] = "inject", ["sub_label"] = "Injected Sub", ["categories"] = new object?[] { "Inj A", "Inj B" } },
            new(StringComparer.Ordinal) { ["template_key"] = "keeptest", ["mode"] = "other", ["sub_label"] = "Skip Mode", ["categories"] = new object?[] { "No" } },
            new(StringComparer.Ordinal) { ["template_key"] = "other", ["mode"] = "inject", ["sub_label"] = "Wrong Tpl", ["categories"] = new object?[] { "No" } }
        ];
        PhpPlanQ1Keep.IndustryGroups = () =>
        [
            new(StringComparer.Ordinal) { ["template_key"] = "keeptest", ["available_sub_areas"] = new object?[] { "Fallback One" } },
            new(StringComparer.Ordinal) { ["template_key"] = "emptytpl", ["available_sub_areas"] = new object?[] { "Only Fallback" } },
            new(StringComparer.Ordinal) { ["template_key"] = "", ["available_sub_areas"] = new object?[] { "Nope" } },
            new(StringComparer.Ordinal) { ["template_key"] = "keeptest", ["available_sub_areas"] = new object?[] { "Dup" } }
        ];
        try
        {
            var subs = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubIndustries("keeptest");
            var again = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubIndustries("keeptest");
            var blank = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubIndustries("");
            var missing = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubIndustries("nonesuch");
            var c1 = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubCategories("keeptest", "Keep Alpha");
            var c2 = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubCategories("keeptest", "Keep Beta & bio");
            var c3 = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubCategories("keeptest", "Injected Sub");
            var c4 = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubCategories("keeptest", "");
            var c5 = PhpPlanQ1Keep.EpcIndustrySeoTemplateSubCategories("", "Keep Alpha");
            var map = PhpPlanQ1Keep.EpcIndustrySeoSitemapEntries();
            return new object?[] { subs, again, blank, missing, c1, c2, c3, c4, c5, map };
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static object? BocPure()
    {
        var ids = new object?[]
        {
            PhpPlanQ1Keep.EpcBocPlatformGroupIds(),
            PhpPlanQ1Keep.EpcBocTenantGroupIds(),
            PhpPlanQ1Keep.EpcBocTenantAreaIds(),
            PhpPlanQ1Keep.EpcBocTenantModePlatformStrip()
        };
        var t1 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme", ["hostname"] = "acme.test", ["label"] = "Acme", ["type"] = "commerce", ["cp_url"] = "" };
        var t2 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "demo_x", ["hostname"] = "d.test", ["label"] = "Demo", ["type"] = "demo", ["cp_url"] = "" };
        var t3 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "demo_y", ["hostname"] = "d.test", ["label"] = "Demo2", ["type"] = "commerce", ["cp_url"] = "/cp/demo/demo_y/" };
        var t4 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "erp1", ["hostname"] = "erp.test", ["label"] = "ERP", ["type"] = "erp_only", ["cp_url"] = "" };
        var t5 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "x", ["hostname"] = "", ["label"] = "NoHost", ["type"] = "commerce", ["cp_url"] = "" };
        var t6 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "demo_z", ["hostname"] = "z.test", ["label"] = "Z", ["type"] = "demo", ["cp_url"] = "https://shop.example/cp/" };
        var urls = new object?[]
        {
            PhpPlanQ1Keep.EpcBocTenantModuleUrl(t1, "/shop/orders"),
            PhpPlanQ1Keep.EpcBocTenantModuleUrl(t2, "shop/x"),
            PhpPlanQ1Keep.EpcBocTenantModuleUrl(t3, "a"),
            PhpPlanQ1Keep.EpcBocTenantModuleUrl(t4, "erp"),
            PhpPlanQ1Keep.EpcBocTenantModuleUrl(t5, "x"),
            PhpPlanQ1Keep.EpcBocTenantModuleUrl(t6, "y")
        };
        var nav = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["command"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["group"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Command" },
                ["areas"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["home"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "control", ["hint"] = "" }
                }
            },
            ["shop"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["group"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Shop" },
                ["areas"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["orders"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/orders", ["hint"] = "OMS" }
                }
            },
            ["commerce"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["group"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Commerce" },
                ["areas"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["cp_marketing"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/marketing", ["hint"] = "Mkt" },
                    ["api"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "control/api", ["hint"] = "API" }
                }
            },
            ["growth"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["group"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Growth" },
                ["areas"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["cp_seo"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/seo", ["hint"] = "" }
                }
            }
        };
        var plat = PhpPlanQ1Keep.EpcBocNavApplyTenantScope(nav, null);
        var ten = PhpPlanQ1Keep.EpcBocNavApplyTenantScope(nav, t1);
        var href1 = PhpPlanQ1Keep.EpcBocAreaHref(new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/x" }, "/cp");
        var href2 = PhpPlanQ1Keep.EpcBocAreaHref(new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/x", ["url_override"] = "https://t/cp/x" }, "/cp");
        var href3 = PhpPlanQ1Keep.EpcBocAreaHref(new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/x", ["url_override"] = "0" }, "/cp");
        var href4 = PhpPlanQ1Keep.EpcBocAreaHref(new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = "shop/x", ["url_override"] = "" }, "/cp");
        var lab1 = PhpPlanQ1Keep.EpcBocScopeLabel(null);
        var lab2 = PhpPlanQ1Keep.EpcBocScopeLabel(t1);
        PhpPlanQ1Keep.EpcBocSessionBoot();
        return new object?[] { ids, urls, plat, ten, href1, href2, href3, href4, lab1, lab2 };
    }

    private static object? BocHtml()
    {
        PhpPlanQ1Keep.SuperCpHost = true;
        PhpPlanQ1Keep.Server["REQUEST_URI"] = "/cp/control?foo=1";
        PhpPlanQ1Keep.TenantList = _ => TenantRows();
        var none = PhpPlanQ1Keep.EpcBocSwitcherTenants(null);
        var pdo = new object();
        var list = PhpPlanQ1Keep.EpcBocSwitcherTenants(pdo);
        var miss = PhpPlanQ1Keep.EpcBocActiveTenant();
        PhpPlanQ1Keep.EpcBocHandleTenantSwitch(pdo);
        var still = PhpPlanQ1Keep.EpcBocActiveTenant();
        PhpPlanQ1Keep.EpcBocSetActiveTenant(list[0]);
        var on = PhpPlanQ1Keep.EpcBocActiveTenant();
        var html1 = PhpPlanQ1Keep.EpcBocRenderTenantSwitcherHtml(pdo);
        PhpPlanQ1Keep.EpcBocClearActiveTenant();
        var html2 = PhpPlanQ1Keep.EpcBocRenderTenantSwitcherHtml(pdo);
        PhpPlanQ1Keep.SuperCpHost = false;
        var html3 = PhpPlanQ1Keep.EpcBocRenderTenantSwitcherHtml(pdo);
        var lab = PhpPlanQ1Keep.EpcBocScopeLabel();
        return new object?[] { none, list, miss, still, on, html1, html2, html3, lab };
    }

    private static List<Dictionary<string, object?>> TenantRows()
        =>
        [
            new(StringComparer.Ordinal) { ["site_key"] = "ecomae", ["hostname"] = "ecomae.com", ["trade_name"] = "Plat", ["type"] = "commerce", ["status"] = "on" },
            new(StringComparer.Ordinal) { ["site_key"] = "skiphost", ["hostname"] = "www.ecomae.com", ["trade_name"] = "X", ["type"] = "commerce", ["status"] = "on" },
            new(StringComparer.Ordinal) { ["site_key"] = "plat", ["hostname"] = "p.example", ["trade_name"] = "P", ["type"] = "platform", ["status"] = "on" },
            new(StringComparer.Ordinal) { ["site_key"] = "acme", ["hostname"] = "www.acme.test", ["trade_name"] = "Acme's Co", ["type"] = "commerce", ["status"] = "live" },
            new(StringComparer.Ordinal) { ["site_key"] = "demo_x", ["hostname"] = "demo.test", ["hub_name"] = "Demo X", ["type"] = "demo", ["status"] = "on" },
            new(StringComparer.Ordinal) { ["site_key"] = "erp1", ["hostname"] = "erp.test", ["trade_name"] = "", ["type"] = "erp_only", ["status"] = "on" },
            new(StringComparer.Ordinal) { ["site_key"] = "", ["hostname"] = "x.test", ["trade_name"] = "Blank", ["type"] = "commerce", ["status"] = "on" }
        ];

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
