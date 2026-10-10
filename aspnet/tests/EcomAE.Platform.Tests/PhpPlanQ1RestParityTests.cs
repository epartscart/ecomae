using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1RestParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Rest");

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
    public void PlanQ1Rest_MatchPhpGolden()
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
                PhpPlanQ1Rest.DemandCountryIsoPath,
                PhpPlanQ1Rest.StorefrontIndustryThemesPath,
                PhpPlanQ1Rest.CpBrochureTopicPhotosPath,
                PhpPlanQ1Rest.PortalThemeTemplatesPath,
                PhpPlanQ1Rest.PortalStorefrontPackagesPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Rest.DemandCountryIsoPath,
                PhpPlanQ1Rest.StorefrontIndustryThemesPath,
                PhpPlanQ1Rest.CpBrochureTopicPhotosPath,
                PhpPlanQ1Rest.PortalThemeTemplatesPath,
                PhpPlanQ1Rest.PortalStorefrontPackagesPath
            });

    [Fact]
    public void DemandMaps_DoesNotStartASession()
    {
        Assert.Equal("ARE", PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("AE"));
        Assert.Equal("", PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("ae"));
    }

    private static string Render(string name)
        => name switch
        {
            "demand_maps" => Json(DemandMaps()),
            "demand_csv_parse" => Json(DemandCsvParse()),
            "demand_csv_preview" => Json(DemandCsvPreview()),
            "demand_migrate" => Json(DemandMigrate()),
            "demand_import" => Json(DemandImport()),
            "themes_data" => Json(ThemesData()),
            "brochure_topics" => Json(BrochureTopics()),
            "theme_slots" => Json(ThemeSlots()),
            "packages_data" => Json(PackagesData()),
            _ => "unknown:" + name
        };

    private static object?[] DemandMaps()
        => new object?[]
        {
            JsonSerializer.Deserialize<object>(PhpPlanQ1Rest.EpcDemandIso2ToIso3Map().GetRawText()),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Rest.EpcDemandCountryRegistry().GetRawText()),
            PhpPlanQ1Rest.EpcDemandIsStockPoolCountryCode("ARE"),
            PhpPlanQ1Rest.EpcDemandIsStockPoolCountryCode("AE"),
            PhpPlanQ1Rest.EpcDemandIsStockPoolCountryCode("SDN"),
            PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("ae"),
            PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("AE"),
            PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("SDN"),
            PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("sdn"),
            PhpPlanQ1Rest.EpcDemandNormalizeCountryCode("XX"),
            PhpPlanQ1Rest.EpcDemandNormalizeCountryCode(""),
            PhpPlanQ1Rest.EpcDemandParseCountryCodesString("SD, DZA; ke | AE"),
            PhpPlanQ1Rest.EpcDemandParseCountryCodesString(""),
            PhpPlanQ1Rest.EpcDemandFormatCountriesDisplay(new object[] { "ke", "SDN", "xx", "AE" })
        };

    private static object?[] DemandCsvParse()
        => new object?[]
        {
            PhpPlanQ1Rest.EpcDemandCsvParseRow(new[] { "brand", "article", "countries" }, new[] { "Bosch", "0986", "SD,KE" }),
            PhpPlanQ1Rest.EpcDemandCsvParseRow(new[] { "manufacturer", "part_number", "country_a", "country_b", "x" }, new[] { "ACME", "AB-12", "EG", "ae", "" }),
            PhpPlanQ1Rest.EpcDemandCsvParseRow(new[] { "make", "sku", "demand" }, new[] { "X", "1", "NGA;SAU" })
        };

    private static object?[] DemandCsvPreview()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecomae_cpw_csv_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var ok = Path.Combine(dir, "ok.csv");
            File.WriteAllText(ok, "brand,article,countries\nBosch,0986,SD;KE\n\nACME,AB12,EG\n");
            var p = PhpPlanQ1Rest.EpcDemandCsvPreviewFile(ok);
            var missing = PhpPlanQ1Rest.EpcDemandCsvPreviewFile(Path.Combine(dir, "nope.csv"));
            var empty = Path.Combine(dir, "empty.csv");
            File.WriteAllText(empty, "");
            var e = PhpPlanQ1Rest.EpcDemandCsvPreviewFile(empty);
            return new object?[] { p, missing, e };
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static object?[] DemandMigrate()
    {
        var db = new PhpPlanQ1Rest.DemandCountryStore { CodeType = "char(3)" };
        db.Countries.Add(new PhpPlanQ1Rest.DemandCountryRow { Code = "SDN", Name = "Sudan", SortOrder = 10 });
        PhpPlanQ1Rest.EpcDemandMigrateCountryCodesToIso3(db);
        PhpPlanQ1Rest.EpcDemandMigrateCountryCodesToIso3(db);
        return new object?[]
        {
            db.Countries.OrderBy(r => r.Code, StringComparer.Ordinal).Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = r.Code,
                ["name"] = r.Name,
                ["sort_order"] = r.SortOrder
            }).ToList()
        };
    }

    private static object?[] DemandImport()
    {
        var db = new PhpPlanQ1Rest.DemandCountryStore();
        var dir = Path.Combine(Path.GetTempPath(), "ecomae_cpw_imp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var f = Path.Combine(dir, "in.csv");
            File.WriteAllText(f, "brand,article,countries\nBosch,0986,SD;KE\nACME,xx,XX\nZed,AB-1,AE;EG\n");
            var r1 = PhpPlanQ1Rest.EpcDemandCsvImportFile(db, f, "merge", a =>
            {
                var cleaned = new string(a.Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
                return cleaned.ToUpperInvariant();
            }, _ => { });
            var rows = db.Articles
                .OrderBy(r => r.Manufacturer, StringComparer.Ordinal)
                .ThenBy(r => r.CountryCode, StringComparer.Ordinal)
                .Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manufacturer"] = r.Manufacturer,
                    ["article_norm"] = r.ArticleNorm,
                    ["country_code"] = r.CountryCode,
                    ["source"] = r.Source
                }).ToList();
            File.WriteAllText(f, "brand,article,countries\nBosch,0986,NGA\n");
            var r2 = PhpPlanQ1Rest.EpcDemandCsvImportFile(db, f, "replace", a =>
            {
                var cleaned = new string(a.Where(ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9').ToArray());
                return cleaned.ToUpperInvariant();
            }, _ => { });
            var rows2 = db.Articles
                .OrderBy(r => r.Manufacturer, StringComparer.Ordinal)
                .ThenBy(r => r.CountryCode, StringComparer.Ordinal)
                .Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manufacturer"] = r.Manufacturer,
                    ["article_norm"] = r.ArticleNorm,
                    ["country_code"] = r.CountryCode
                }).ToList();
            var bad = PhpPlanQ1Rest.EpcDemandCsvImportFile(db, Path.Combine(dir, "nope.csv"), "merge");
            return new object?[] { r1, rows, r2, rows2, bad };
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static object?[] ThemesData()
    {
        var reg = PhpPlanQ1Rest.EpcIndustryThemeRegistry();
        var t = PhpPlanQ1Rest.EpcIndustryTheme("automotive");
        var d = PhpPlanQ1Rest.EpcIndustryTheme("nope_group");
        var def = PhpPlanQ1Rest.EpcIndustryThemeDefault("nope_group");
        var kit = PhpPlanQ1Rest.EpcErpIndustryKit("automotive");
        var kitDef = PhpPlanQ1Rest.EpcErpIndustryKit("nope_group");
        var al = PhpPlanQ1Rest.EpcCpIndustryAlignment("automotive");
        var alDef = PhpPlanQ1Rest.EpcCpIndustryAlignment("nope_group");
        return new object?[]
        {
            reg.EnumerateObject().Select(p => p.Name).ToArray(),
            t.GetProperty("hero").GetProperty("tagline").GetString(),
            t.GetProperty("hero").GetProperty("accent").GetString(),
            t.GetProperty("categories").GetArrayLength(),
            t.GetProperty("products").GetArrayLength(),
            JsonSerializer.Deserialize<object>(t.GetProperty("erp_kit").GetRawText()),
            JsonSerializer.Deserialize<object>(d.GetRawText()),
            JsonSerializer.Deserialize<object>(def.GetRawText()),
            kit.GetProperty("label").GetString(),
            kit.GetProperty("costing_method").GetString(),
            JsonSerializer.Deserialize<object>(kit.GetProperty("modules").GetRawText()),
            kitDef.GetProperty("label").GetString(),
            JsonSerializer.Deserialize<object>(kitDef.GetProperty("modules").GetRawText()),
            al.GetProperty("catalog_display").GetString(),
            al.GetProperty("price_display").GetString(),
            alDef.GetProperty("catalog_display").GetString()
        };
    }

    private static object?[] BrochureTopics()
    {
        var cat = PhpPlanQ1Rest.EpcCpBrochureTopicCatalog();
        var areas = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal)
        {
            ["Shop"] = new()
            {
                new(StringComparer.Ordinal) { ["id"] = "p1", ["name"] = "AI Copilot", ["url"] = "/ai", ["does"] = "" },
                new(StringComparer.Ordinal) { ["id"] = "p2", ["name"] = "Warehouse stock", ["url"] = "/wh", ["does"] = "inventor shelves" },
                new(StringComparer.Ordinal) { ["name"] = "Mystery" }
            }
        };
        var map = PhpPlanQ1Rest.EpcCpBrochureAssignUniquePhotos(areas);
        var photo = PhpPlanQ1Rest.EpcCpBrochureItemTopicPhoto(areas["Shop"][0], "Shop");
        var photo2 = PhpPlanQ1Rest.EpcCpBrochureItemTopicPhoto(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "z",
                ["name"] = "Mystery",
                ["image"] = "https://example.com/x.jpg",
                ["photo_label"] = "Money",
                ["photo_topic"] = "money"
            },
            "x");
        return new object?[]
        {
            cat.EnumerateObject().Select(p => p.Name).ToArray(),
            cat.GetProperty("ai").GetProperty("label").GetString(),
            cat.GetProperty("autoparts").GetProperty("photos").GetArrayLength(),
            PhpPlanQ1Rest.EpcCpBrochureTopicSvgUrl("AI Copilot!", "id 1", "AI Copilot", "Shop"),
            PhpPlanQ1Rest.EpcCpBrochureTopicSvgUrl("", "", "", ""),
            PhpPlanQ1Rest.EpcCpBrochureResolveTopic("AI Parts Expert"),
            PhpPlanQ1Rest.EpcCpBrochureResolveTopic("Price upload warehouse"),
            PhpPlanQ1Rest.EpcCpBrochureResolveTopic("Something", "finance"),
            PhpPlanQ1Rest.EpcCpBrochureResolveTopic("Hello"),
            PhpPlanQ1Rest.EpcCpBrochureResolveTopic("VIN decode catalog", "shop", "/vin", "does text"),
            areas,
            map,
            photo,
            photo2
        };
    }

    private static object?[] ThemeSlots()
    {
        var industries = PhpPlanQ1Rest.ThemeSlotIndustries();
        var pal = PhpPlanQ1Rest.EpcPortalThemePalette("#1", "#2", "#3", "#4", "#5", "#6", "#7");
        var style = PhpPlanQ1Rest.EpcPortalThemeStyle("classic", "L", "D", pal);
        var defs = PhpPlanQ1Rest.EpcPortalThemePaletteDefinitions();
        var q = PhpPlanQ1Rest.EpcPortalQuartetFromBaseTheme(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["primary"] = "#111111",
                ["primary_dark"] = "#222222",
                ["accent"] = "#333333",
                ["sidebar_from"] = "#444444",
                ["sidebar_to"] = "#555555",
                ["hero_from"] = "#666666",
                ["hero_to"] = "#777777"
            },
            "Unknown Vert");
        var tr = PhpPlanQ1Rest.EpcPortalTripletFromBaseTheme(new Dictionary<string, string>(StringComparer.Ordinal) { ["primary"] = "#111111" });
        var all = PhpPlanQ1Rest.EpcPortalIndustryStyleTemplates(industries);
        var js = PhpPlanQ1Rest.EpcPortalStyleTemplatesForJs(industries);
        var site = PhpPlanQ1Rest.EpcPortalResolveSiteTheme(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["industry_code"] = "electronics", ["theme_template"] = "midnight" },
            industries);
        return new object?[]
        {
            PhpPlanQ1Rest.EpcPortalThemeTemplateSlotIds(),
            PhpPlanQ1Rest.EpcPortalDefaultThemeTemplateByIndustry().GetProperty("auto_parts").GetString(),
            PhpPlanQ1Rest.EpcPortalDefaultThemeTemplateByIndustry().GetProperty("electronics").GetString(),
            pal,
            style["label"],
            defs.EnumerateObject().Select(p => p.Name).ToArray(),
            defs.GetProperty("auto_parts")[0].GetProperty("label").GetString(),
            q[0]["label"],
            ((Dictionary<string, string>)q[1]["theme"]!)["sidebar_from"],
            tr.Keys.ToArray(),
            all.Keys.ToArray(),
            PhpPlanQ1Rest.EpcPortalStyleTemplateIds("auto_parts", industries),
            PhpPlanQ1Rest.EpcPortalStyleTemplateIds("unknown_vert", industries),
            PhpPlanQ1Rest.EpcPortalNormalizeThemeTemplate("auto_parts", "dark", industries),
            PhpPlanQ1Rest.EpcPortalNormalizeThemeTemplate("auto_parts", "nope", industries),
            PhpPlanQ1Rest.EpcPortalDefaultThemeTemplate("electronics", industries),
            PhpPlanQ1Rest.EpcPortalDefaultThemeTemplate("zzz", industries),
            PhpPlanQ1Rest.EpcPortalStyleTemplateTheme("auto_parts", "midnight", industries)["primary"],
            PhpPlanQ1Rest.EpcPortalStyleTemplateMeta("auto_parts", "classic", industries),
            js.Keys.ToArray(),
            site["primary"]
        };
    }

    private static object?[] PackagesData()
    {
        var industries = PhpPlanQ1Rest.PackageIndustries();
        var reg = PhpPlanQ1Rest.EpcPortalStorefrontPackageRegistry();
        var meta = PhpPlanQ1Rest.EpcPortalStorefrontPackageMeta("electronics_retail_virgin");
        var miss = PhpPlanQ1Rest.EpcPortalStorefrontPackageMeta("nope!");
        var js = PhpPlanQ1Rest.EpcPortalStorefrontPackagesForJs();
        var settings = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tagline"] = "keep me" };
        var contact = new Dictionary<string, object?>(StringComparer.Ordinal);
        var ap = PhpPlanQ1Rest.EpcPortalApplyIndustryThemeProfile(settings, contact, "electronics", null, industries);
        var settings2 = new Dictionary<string, object?>(StringComparer.Ordinal);
        var contact2 = new Dictionary<string, object?>(StringComparer.Ordinal);
        var ap2 = PhpPlanQ1Rest.EpcPortalApplyIndustryThemeProfile(settings2, contact2, "auto_parts", null, industries);
        var settings3 = new Dictionary<string, object?>(StringComparer.Ordinal);
        var contact3 = new Dictionary<string, object?>(StringComparer.Ordinal);
        var ap3 = PhpPlanQ1Rest.EpcPortalApplyIndustryThemeProfile(settings3, contact3, "erp_standalone", null, industries);
        var pre = PhpPlanQ1Rest.EpcPortalStorefrontPackagePreset("jewellery_retail_kiyasha", industries);
        return new object?[]
        {
            reg.EnumerateObject().Select(p => p.Name).ToArray(),
            meta!.Value.GetProperty("label").GetString(),
            meta.Value.GetProperty("theme_template").GetString(),
            miss is null ? null : "x",
            PhpPlanQ1Rest.EpcPortalStorefrontPackageForIndustry("fashion"),
            PhpPlanQ1Rest.EpcPortalStorefrontPackageForIndustry("nope"),
            PhpPlanQ1Rest.EpcPortalStorefrontPackageImplementedIds(),
            js["consulting_primeinvest"]["label"],
            PhpPlanQ1Rest.EpcPortalResolveStorefrontPackage(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["storefront_package"] = "fashion_retail_namshi" }
            }),
            PhpPlanQ1Rest.EpcPortalResolveStorefrontPackage(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["industry_code"] = "electronics",
                ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            }),
            PhpPlanQ1Rest.EpcPortalResolveStorefrontPackage(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["industry_code"] = "other",
                ["contact"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            }),
            pre["storefront_package"],
            pre["theme_template"],
            ((Dictionary<string, string>)pre["theme"]!)["primary"],
            ap,
            settings["industry_code"],
            settings["theme_template"],
            contact["storefront_package"],
            contact["use_tenant_brand"],
            ap2,
            contact2["use_animated_hub_logo"],
            contact2["use_tenant_brand"],
            ap3,
            contact3.ContainsKey("storefront_package"),
            contact3["use_tenant_brand"]
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
