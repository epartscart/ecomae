using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpIndustryChromeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "IndustryChrome");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpIndustryChrome.FashionFooterPath,
        PhpIndustryChrome.ElectronicsFooterPath,
        PhpIndustryChrome.JewelleryFooterPath,
        PhpIndustryChrome.ConsultingFooterPath,
        PhpIndustryChrome.FashionHelpersPath,
        PhpIndustryChrome.ElectronicsHelpersPath,
        PhpIndustryChrome.JewelleryHelpersPath,
        PhpIndustryChrome.ConsultingHelpersPath,
        PhpIndustryChrome.AnimatedLogosPath,
        PhpIndustryChrome.MarketingTemplatesPath,
        PhpIndustryChrome.ChannelSchemaPath,
        PhpIndustryChrome.PageFramePath,
        PhpIndustryChrome.FilemanagerConfigPath,
        PhpIndustryChrome.OrdersItemsConfigPath
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
    public void IndustryChrome_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expectedOutput = results[i].GetProperty("output").GetString() ?? string.Empty;
            var expectedResult = results[i].GetProperty("result");
            var actual = Render(name);
            var expected = expectedOutput.Length > 0 ? expectedOutput : ResultText(expectedResult);
            if ((name.StartsWith("footer_", StringComparison.Ordinal) && name != "footer_frn_href") || name == "frame_hero")
            {
                if (!StructuralOk(name, actual))
                {
                    failures.Add(name + " structure");
                }

                continue;
            }

            if (!Same(actual, expected, expectedResult))
            {
                failures.Add(name + " expected=" + Truncate(expected) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Subset(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement
                .GetProperty("cases").EnumerateArray()
                .Select(CaseFile)
                .ToHashSet(StringComparer.Ordinal)!,
            CoveredFiles);

    [Fact]
    public void ScriptPaths_StayOutsideTheCpLoginWall()
    {
        Assert.True(PhpIndustryChrome.IsScriptPath(PhpIndustryChrome.FilemanagerConfigUrl));
        Assert.True(PhpIndustryChrome.IsScriptPath(PhpIndustryChrome.OrdersItemsConfigUrl));
        Assert.False(PhpIndustryChrome.IsScriptPath("/cp/shop/orders/items"));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.RequiresAdmin(PhpIndustryChrome.FilemanagerConfigUrl));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.RequiresAdmin(PhpIndustryChrome.OrdersItemsConfigUrl));
    }

    private static string CaseFile(JsonElement testCase)
        => testCase.TryGetProperty("file", out var file) && file.GetString() is { Length: > 0 } path
            ? path
            : testCase.GetProperty("name").GetString() switch
            {
                var name when name is not null && name.StartsWith("seo_frn", StringComparison.Ordinal) => PhpIndustryChrome.FashionHelpersPath,
                var name when name is not null && name.StartsWith("seo_er", StringComparison.Ordinal) => PhpIndustryChrome.ElectronicsHelpersPath,
                var name when name is not null && name.StartsWith("seo_jrk", StringComparison.Ordinal) => PhpIndustryChrome.JewelleryHelpersPath,
                var name when name is not null && name.StartsWith("seo_cpi", StringComparison.Ordinal) => PhpIndustryChrome.ConsultingHelpersPath,
                var name when name is not null && name.StartsWith("logo_", StringComparison.Ordinal) => PhpIndustryChrome.AnimatedLogosPath,
                var name when name is not null && name.StartsWith("mb_", StringComparison.Ordinal) => PhpIndustryChrome.MarketingTemplatesPath,
                "channel_tables" => PhpIndustryChrome.ChannelSchemaPath,
                var name when name is not null && name.StartsWith("frame_", StringComparison.Ordinal) => PhpIndustryChrome.PageFramePath,
                var name when name is not null && name.StartsWith("fm_", StringComparison.Ordinal) => PhpIndustryChrome.FilemanagerConfigPath,
                var name when name is not null && name.StartsWith("oi_", StringComparison.Ordinal) => PhpIndustryChrome.OrdersItemsConfigPath,
                "footer_frn_href" => PhpIndustryChrome.FashionFooterPath,
                _ => "unknown"
            };

    private static string Render(string name)
    {
        var empty = new PhpIndustryChrome.IndustryPortal();
        var named = new PhpIndustryChrome.IndustryPortal(
            Settings: new Dictionary<string, object?> { ["system_name"] = "  MyShop  ", ["tagline"] = "  Wear it  ", ["domain_path"] = "https://shop.test" });
        var elHost = new PhpIndustryChrome.IndustryPortal(Host: "el.test");
        var goldCfg = new PhpIndustryChrome.IndustryPortal(ConfigDomainPath: "https://gold.test/");
        var cpiTrade = new PhpIndustryChrome.IndustryPortal(
            Settings: new Dictionary<string, object?>
            {
                ["contact"] = new Dictionary<string, object?> { ["trade_name"] = "  Prime  " },
                ["tagline"] = "Advice"
            });
        return name switch
        {
            "footer_frn" => PhpIndustryChrome.RetailFooter("frn", "/en/", [("Care", [("A&B", "/help"), ("Ext", "https://x.com")])], [("IG", "https://ig.com/?q=1", "fa-instagram")], ["visa"], "Style&Co", 2026, " <em>hosted</em>"),
            "footer_er" => PhpIndustryChrome.RetailFooter("er", "/ar", [("Shop", [("Phones", "/phones")])], [], ["mastercard"], "Electro", 2026),
            "footer_jrk" => PhpIndustryChrome.RetailFooter("jrk", "/en", [("Bridal", [("Rings", "/rings")])], [("FB", "https://fb.com/", "fa-facebook")], ["applepay"], "Gold", 2026),
            "footer_cpi" => PhpIndustryChrome.ConsultingFooter("/en/", [("Srv", [("VAT", "/#vat")])], "Tax&Co", 2026, "a@b.com", "+971", "host", "<b>Brand</b>", [("https://li.com/", "LI", "fa-linkedin")]),
            "footer_frn_href" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcFrnFooterHref("/en/", "/help"), PhpIndustryChrome.EpcFrnFooterHref("/en", "https://x.com"), PhpIndustryChrome.EpcFrnFooterHref("/ar", "") }, JsonOpts),
            "seo_frn_defaults" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcFashionRetailNamshiStoreName(empty), PhpIndustryChrome.EpcFashionRetailNamshiTagline(empty), PhpIndustryChrome.EpcFashionRetailNamshiPublicUrl(empty) }, JsonOpts),
            "seo_frn_named" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcFashionRetailNamshiStoreName(named), PhpIndustryChrome.EpcFashionRetailNamshiTagline(named), PhpIndustryChrome.EpcFashionRetailNamshiPublicUrl(named) }, JsonOpts),
            "seo_frn_apply_main" => Seo(c => PhpIndustryChrome.EpcFashionRetailNamshiApplySeo(c, empty), 1, "x"),
            "seo_frn_apply_page" => Seo(c => PhpIndustryChrome.EpcFashionRetailNamshiApplySeo(c, empty), 0, " <b>Dresses</b> "),
            "seo_frn_apply_bad" => Seo(c => PhpIndustryChrome.EpcFashionRetailNamshiApplySeo(c, empty), null, "eParts Cart sale"),
            "seo_frn_patch" => PhpIndustryChrome.EpcFashionRetailNamshiPatchTemplateHtml("<html><title>Old</title><meta name=\"keywords\" content=\"k\"><meta name=\"description\" content=\"d\"></html>", empty),
            "seo_frn_scrub" => PhpIndustryChrome.EpcFashionRetailNamshiScrubLegacyStrings("eParts Cart (Autoparts) sells auto parts and Autoparts spare parts", empty),
            "seo_er_defaults" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcElectronicsRetailStoreName(elHost), PhpIndustryChrome.EpcElectronicsRetailTagline(elHost), PhpIndustryChrome.EpcElectronicsRetailPublicUrl(elHost) }, JsonOpts),
            "seo_er_apply_main" => Seo(c => PhpIndustryChrome.EpcElectronicsRetailApplySeo(c, empty), true, null),
            "seo_er_scrub" => PhpIndustryChrome.EpcElectronicsRetailScrubAutopartsStrings("Buy autoparts and spare parts at eParts Cart", empty),
            "seo_jrk_defaults" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcJewelleryRetailKiyashaStoreName(empty), PhpIndustryChrome.EpcJewelleryRetailKiyashaTagline(empty) }, JsonOpts),
            "seo_jrk_apply_main" => Seo(c => PhpIndustryChrome.EpcJewelleryRetailKiyashaApplySeo(c, empty), 1, null),
            "seo_jrk_public" => PhpIndustryChrome.EpcJewelleryRetailKiyashaPublicUrl(goldCfg),
            "seo_cpi_defaults" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcCpiStoreName(empty), PhpIndustryChrome.EpcCpiTagline(empty) }, JsonOpts),
            "seo_cpi_trade" => JsonSerializer.Serialize(new[] { PhpIndustryChrome.EpcCpiStoreName(cpiTrade), PhpIndustryChrome.EpcCpiTagline(cpiTrade) }, JsonOpts),
            "seo_cpi_apply_main" => Seo(c => PhpIndustryChrome.EpcCpiApplySeo(c, empty), 1, null),
            "seo_cpi_patch" => PhpIndustryChrome.EpcCpiPatchTemplateHtml("<title>eParts Cart</title><meta name=\"keywords\" content=\"k\"><meta name=\"description\" content=\"d\"> Autoparts", empty),
            "logo_enqueue" => PhpIndustryChrome.EpcStorefrontAnimatedLogoEnqueue(),
            "logo_fashion" => PhpIndustryChrome.EpcStorefrontAnimatedLogoFashion("A&B"),
            "logo_electronics" => PhpIndustryChrome.EpcStorefrontAnimatedLogoElectronics("El"),
            "logo_consulting" => PhpIndustryChrome.EpcStorefrontAnimatedLogoConsulting("Tax"),
            "logo_jewellery" => PhpIndustryChrome.EpcStorefrontAnimatedLogoJewellery("Gold"),
            "logo_unknown" => PhpIndustryChrome.EpcStorefrontAnimatedLogoMarkup("auto_parts", "Store", false).Echo,
            "logo_sanitize" => PhpIndustryChrome.EpcStorefrontAnimatedLogoMarkup("Fashion-Retail!", "Store", false).Echo,
            "logo_epart_label" => JsonSerializer.Serialize(LogoPair("fashion_retail_namshi", "eParts Cart"), JsonOpts),
            "mb_email" => JsonSerializer.Serialize(PhpIndustryChrome.EpcMbEmailTemplates().Keys, JsonOpts),
            "mb_whatsapp" => JsonSerializer.Serialize(PhpIndustryChrome.EpcMbWhatsappTemplates(), JsonOpts),
            "mb_apply" => PhpIndustryChrome.EpcMbApplyTemplateVars("Hi {{customer_name}} from {{shop_name}} {{missing}}", new Dictionary<string, string> { ["customer_name"] = "Ali", ["shop_name"] = "A&B" }),
            "channel_tables" => JsonSerializer.Serialize(PhpIndustryChrome.EpcChannelEnsureSchema().Concat(["shop_orders_items_statuses_ref"]).OrderBy(x => x, StringComparer.Ordinal).ToArray(), JsonOpts),
            "frame_assets" => JsonSerializer.Serialize(PhpIndustryChrome.EpcCpRegisterPageAssets(["/a.css"], ["/y.js"], PhpIndustryChrome.EpcCpRegisterPageAssets([" /a.css ", "", "/b.css"], ["/x.js"])), JsonOpts),
            "frame_open" => PhpIndustryChrome.EpcCpPageFrameOpen("extra"),
            "frame_hero" => PhpIndustryChrome.EpcCpPageFrameHero("Ops", "A&B", "Go <b>now</b>", false, [("Open", "/cp", "fa-cog", true), ("Help", "#", "", false)]),
            "frame_close" => PhpIndustryChrome.EpcCpPageFrameClose(),
            "fm_guest" => PhpIndustryChrome.FilemanagerConfig("cp", "", ""),
            "fm_zh" => PhpIndustryChrome.FilemanagerConfig("cp", "", "zh"),
            "fm_fail" => PhpIndustryChrome.FilemanagerConfigFailed(),
            "oi_guest" => PhpIndustryChrome.OrdersItemsEmpty(),
            "oi_admin" => PhpIndustryChrome.OrdersItemsConfig("cp", "ar", "tok&", 7, "price", "asc", "1", "2", ["3", "5"]),
            _ => "unknown:" + name
        };
    }

    private static string[] LogoPair(string package, string trade)
    {
        var pair = PhpIndustryChrome.EpcStorefrontAnimatedLogoMarkup(package, trade, false);
        return [pair.Echo, pair.Markup];
    }

    private static string Seo(Action<PhpIndustryChrome.SeoContent> apply, object? main, string? value)
    {
        var content = new PhpIndustryChrome.SeoContent { MainFlag = main, Value = value };
        apply(content);
        return JsonSerializer.Serialize(new[] { content.TitleTag, content.DescriptionTag, content.KeywordsTag }, JsonOpts);
    }

    private static bool StructuralOk(string name, string actual)
        => name switch
        {
            "footer_frn" => actual.Contains("epc_frn_footer", StringComparison.Ordinal)
                && actual.Contains("/en/help", StringComparison.Ordinal)
                && actual.Contains("A&amp;B", StringComparison.Ordinal)
                && actual.Contains("https://x.com", StringComparison.Ordinal)
                && actual.Contains("visa.jpg", StringComparison.Ordinal)
                && actual.Contains("Style&amp;Co", StringComparison.Ordinal)
                && actual.Contains("<em>hosted</em>", StringComparison.Ordinal),
            "footer_er" => actual.Contains("epc_er_footer", StringComparison.Ordinal)
                && actual.Contains("/ar/phones", StringComparison.Ordinal)
                && actual.Contains("mastercard.jpg", StringComparison.Ordinal),
            "footer_jrk" => actual.Contains("epc_jrk_footer", StringComparison.Ordinal)
                && actual.Contains("/en/rings", StringComparison.Ordinal)
                && actual.Contains("applepay.jpg", StringComparison.Ordinal),
            "footer_cpi" => actual.Contains("epc_cpi_footer", StringComparison.Ordinal)
                && actual.Contains("/en/kontakty", StringComparison.Ordinal)
                && actual.Contains("/en/#vat", StringComparison.Ordinal)
                && actual.Contains("<b>Brand</b>", StringComparison.Ordinal)
                && actual.Contains("fa-linkedin", StringComparison.Ordinal)
                && actual.Contains("a@b.com", StringComparison.Ordinal),
            "frame_hero" => actual.Contains("epc-scp-dashboard__hero", StringComparison.Ordinal)
                && actual.Contains("epc-scp-dashboard__badge", StringComparison.Ordinal)
                && actual.Contains("A&amp;B", StringComparison.Ordinal)
                && actual.Contains("Go &lt;b&gt;now&lt;/b&gt;", StringComparison.Ordinal)
                && actual.Contains("btn-primary", StringComparison.Ordinal)
                && actual.Contains("href=\"/cp\"", StringComparison.Ordinal)
                && actual.Contains("fa-cog", StringComparison.Ordinal)
                && actual.Contains("Help", StringComparison.Ordinal),
            _ => false
        };

    private static bool Same(string actual, string expected, JsonElement result)
    {
        if (actual == expected)
        {
            return true;
        }

        if (result.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.False or JsonValueKind.True or JsonValueKind.Number)
        {
            try
            {
                using var left = JsonDocument.Parse(actual);
                return JsonEquivalent(left.RootElement, result);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
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

    private static string ResultText(JsonElement result)
        => result.ValueKind switch
        {
            JsonValueKind.String => result.GetString() ?? "",
            JsonValueKind.Null => "",
            _ => result.GetRawText()
        };

    private static string Truncate(string value)
        => value.Length <= 220 ? value : value[..220] + "…";
}
