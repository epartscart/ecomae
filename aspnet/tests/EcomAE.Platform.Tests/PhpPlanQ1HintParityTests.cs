using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HintParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hint");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hint_MatchPhpGolden()
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
                var exp = expected.GetRawText();
                var got = Json(actual);
                failures.Add(name + " " + FirstDiff(exp, got));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/general_pages/epc_cata_config.php", PhpPlanQ1Hint.CataConfigPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hint.Reset();
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        Assert.True(PhpPlanQ1Hint.EpcCataEnabledForRequest());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hint.CataConfigPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Hint.EpcCataStorefrontUrl(), StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Hint.CataConfigPath, StringComparison.Ordinal);
        Assert.Equal("/en/eparts-cata", PhpPlanQ1Hint.EpcCataStorefrontUrl());
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hint.Reset();
        return name switch
        {
            "empty" => Empty(),
            "gates" => Gates(),
            "live" => Live(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var labels = PhpPlanQ1Hint.EpcCataProviders().ToDictionary(kv => kv.Key, kv => (string)kv.Value["label"]!, StringComparer.Ordinal);
        return new object[]
        {
            PhpPlanQ1Hint.EpcCataProviderIds(),
            labels,
            PhpPlanQ1Hint.EpcCataNormalizeProvider("local"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("tecdoc"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("eparts"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("partsapi"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("umapi"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("nope"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("Local-TecDoc"),
            PhpPlanQ1Hint.EpcCataNormalizeProvider("LOCAL"),
            PhpPlanQ1Hint.EpcCataTables(),
            PhpPlanQ1Hint.EpcCataVersion,
            PhpPlanQ1Hint.EpcCataSettingsTable(),
            PhpPlanQ1Hint.EpcCataRouteUrl(),
            PhpPlanQ1Hint.EpcCataCatalogTargets().Keys.ToList(),
            PhpPlanQ1Hint.EpcCataSanitizeCategoryName("", 4),
            PhpPlanQ1Hint.EpcCataSanitizeCategoryName("???", 7),
            PhpPlanQ1Hint.EpcCataSanitizeCategoryName("Filters", 2),
            PhpPlanQ1Hint.EpcCataSanitizeCategoryName("?", 0),
            PhpPlanQ1Hint.EpcCataStorefrontSlug("Brake System"),
            PhpPlanQ1Hint.EpcCataStorefrontSlug("Fuel Mixture Formation"),
            PhpPlanQ1Hint.EpcCataStorefrontSlug("  Air  Conditioning "),
            PhpPlanQ1Hint.EpcCataNormalizeVin(""),
            PhpPlanQ1Hint.EpcCataNormalizeVin("WBA12345678"),
            PhpPlanQ1Hint.EpcCataNormalizeVin("wba12345678"),
            PhpPlanQ1Hint.EpcCataNormalizeVin("WBA-123 456789"),
            PhpPlanQ1Hint.EpcCataNormalizeVin("SHORT"),
            PhpPlanQ1Hint.EpcCataEnsureTables() ? 1 : 0,
            PhpPlanQ1Hint.EpcCataSettingsEnsure() ? 1 : 0,
            PhpPlanQ1Hint.EpcCataSettingSet("k", "v") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataLocalCatalogPdo() is null ? 1 : 0,
            PhpPlanQ1Hint.EpcCataDb() is null ? 1 : 0,
            PhpPlanQ1Hint.EpcCataLegacyBrowseCounts(),
            PhpPlanQ1Hint.EpcCataLegacyVinCounts(),
            PhpPlanQ1Hint.EpcCataPresentationDefaults()["default_section"]!,
            PhpPlanQ1Hint.EpcCataPresentationDefaults()["default_article_view"]!,
            PhpPlanQ1Hint.EpcCataProvidersEnabledMap(),
            PhpPlanQ1Hint.EpcCataCategoryConfigSettingKey(),
            PhpPlanQ1Hint.EpcCataNormalizeCatalogTarget("eparts-cata"),
            PhpPlanQ1Hint.EpcCataNormalizeCatalogTarget("EPARTS-CATA"),
            PhpPlanQ1Hint.EpcCataNormalizeCatalogTarget("nope"),
        };
    }

    private static object Gates()
    {
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        PhpPlanQ1Hint.AutoPartsSite = false;
        var eparts = PhpPlanQ1Hint.EpcCataEnabledForRequest() ? 1 : 0;
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.ecomae.com";
        var other = PhpPlanQ1Hint.EpcCataEnabledForRequest() ? 1 : 0;
        PhpPlanQ1Hint.AutoPartsSite = true;
        var auto = PhpPlanQ1Hint.EpcCataEnabledForRequest() ? 1 : 0;
        PhpPlanQ1Hint.AutoPartsSite = false;
        return new object[]
        {
            eparts,
            other,
            auto,
            PhpPlanQ1Hint.EpcCataProviderEnabled("") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataProviderEnabled("nope") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataProviderEnabled("umapi") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataShouldUseDefaultCategoryTree("") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataShouldUseDefaultCategoryTree("local_tecdoc") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataShouldUseDefaultCategoryTree("umapi") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataShouldUseDefaultCategoryTree("tecdoc") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataProviderOrigin("umapi"),
            PhpPlanQ1Hint.EpcCataProviderOrigin("local"),
            PhpPlanQ1Hint.EpcCataProviderOrigin("eparts"),
            PhpPlanQ1Hint.EpcCataProviderOrigin("carcat"),
            PhpPlanQ1Hint.EpcCataProviderOrigin("laximo"),
            PhpPlanQ1Hint.EpcCataProviderOrigin("levam"),
            PhpPlanQ1Hint.EpcCataProviderOrigin("nope"),
        };
    }

    private static object Live()
    {
        PhpPlanQ1Hint.DocumentRoot = "/workspace-wt/small-done";
        var defaults = PhpPlanQ1Hint.EpcCataDefaultCategories();
        var cached = PhpPlanQ1Hint.EpcCataDefaultCategoriesCached();
        var store = PhpPlanQ1Hint.EpcCataStorefrontCategories();
        var filters = PhpPlanQ1Hint.EpcCataStorefrontResolveCategory("Filters");
        var byId = PhpPlanQ1Hint.EpcCataStorefrontResolveCategory("2");
        var bySlug = PhpPlanQ1Hint.EpcCataStorefrontResolveCategory("brake-system");
        var missing = PhpPlanQ1Hint.EpcCataStorefrontResolveCategory("nope-group");
        var hrefEn = PhpPlanQ1Hint.EpcCataStorefrontCategoryHref("/en", store[0]);
        var hrefAr = PhpPlanQ1Hint.EpcCataStorefrontCategoryHref("/ar/", store[3], "Pads");
        var searchRow = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["STR_ID"] = 2,
            ["CATEGORY_NAME"] = "Filters",
            ["catalog_target"] = "parts-search",
            ["tecdoc_pt_id"] = 99
        };
        var hrefSearch = PhpPlanQ1Hint.EpcCataStorefrontCategoryHref("", searchRow, "Oil Filter");
        var payloadNested = PhpPlanQ1Hint.EpcCataPayloadData(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = new List<object?>
            {
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 1 },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 2 }
            }
        });
        var payloadList = PhpPlanQ1Hint.EpcCataPayloadData(new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = 1 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = 2 }
        });
        var payloadEmpty = PhpPlanQ1Hint.EpcCataPayloadData(new List<object?>());
        var modEmpty = PhpPlanQ1Hint.EpcCataModificationFromRow(new Dictionary<string, object?>(StringComparer.Ordinal));
        var modTitle = PhpPlanQ1Hint.EpcCataModificationFromRow(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = "1.6 TDI",
            ["ext_id"] = 44,
            ["source"] = "umapi",
            ["section"] = "passenger"
        });
        var modRaw = PhpPlanQ1Hint.EpcCataModificationFromRow(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["raw_json"] = "{\"PC_ID\":88,\"MODIFICATION\":\"2.0 TDI\",\"POWER_KW\":\"110\"}",
            ["source"] = "umapi"
        });
        var normRows = PhpPlanQ1Hint.EpcCataNormalizeCategoryRows(new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["STR_ID"] = 9, ["CATEGORY_NAME"] = "???" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["STR_ID"] = 2, ["CATEGORY_NAME"] = "Filters", ["ORDER"] = 1 },
            "bad",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["CATEGORY_NAME"] = "", ["STR_ID"] = 0 }
        });
        var entry = PhpPlanQ1Hint.EpcCataCategoryConfigNormalizeEntry(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["STR_ID"] = 2,
            ["display_name"] = "Oil Filters",
            ["catalog_target"] = "eparts-cata"
        }, 3)!;
        var ajax = PhpPlanQ1Hint.EpcCataCategoryConfigForAjax();
        var jsLight = PhpPlanQ1Hint.EpcCataPresentationJsConfigLight();
        var jsFull = PhpPlanQ1Hint.EpcCataPresentationJsConfig();
        var td = PhpPlanQ1Hint.EpcCataTecdocConfig();
        var local = PhpPlanQ1Hint.EpcCataLocalCatalogConfig();
        var shell = PhpPlanQ1Hint.EpcCataCpStatusShell();
        var cachedStatus = PhpPlanQ1Hint.EpcCataCpStatusCached();
        var mon = PhpPlanQ1Hint.MonitorStable(PhpPlanQ1Hint.EpcCataCpImportMonitor());
        return new object[]
        {
            PhpPlanQ1Hint.CatDigest(defaults),
            PhpPlanQ1Hint.CatDigest(cached),
            PhpPlanQ1Hint.CatDigest(store),
            (string)(store[0]["catalog_target"] ?? ""),
            filters is not null ? 1 : 0,
            byId is not null ? 1 : 0,
            bySlug is not null ? 1 : 0,
            missing is null ? 1 : 0,
            (string)(((Dictionary<string, object?>)filters!["category"]!)["CATEGORY_NAME"] ?? ""),
            (string)(((Dictionary<string, object?>)bySlug!["category"]!)["CATEGORY_NAME"] ?? ""),
            hrefEn,
            hrefAr,
            hrefSearch,
            payloadNested.Count,
            payloadList.Count,
            payloadEmpty.Count,
            modEmpty["title"]!,
            modTitle["title"]!,
            modTitle["ext_id"]!,
            modRaw["title"]!,
            modRaw["PC_ID"]!,
            modRaw["power_kw"]!,
            PhpPlanQ1Hint.CatDigest(normRows),
            entry["CATEGORY_NAME"]!,
            entry["catalog_target"]!,
            entry["ORDER"]!,
            ((List<Dictionary<string, object?>>)ajax["categories"]!).Count,
            PhpPlanQ1Hint.PhpEmpty(ajax["customized"]) ? 0 : 1,
            ajax["storefront_count"]!,
            ajax["icon_base"]!,
            jsLight["version"]!,
            PhpPlanQ1Hint.PhpEmpty(jsLight["categoriesDeferred"]) ? 0 : 1,
            ((List<object>)jsLight["categories"]!).Count,
            jsFull["version"]!,
            PhpPlanQ1Hint.PhpEmpty(jsFull["categoriesDeferred"]) ? 0 : 1,
            (string)(td["data_root"] ?? ""),
            Convert.ToInt32(td["min_year"]),
            (string)(td["table_prefix"] ?? ""),
            PhpPlanQ1Hint.PhpEmpty(td["stream_mode"]) ? 0 : 1,
            (string)(local["table_prefix"] ?? ""),
            (string)(local["host"] ?? ""),
            PhpPlanQ1Hint.PhpEmpty(shell["ok"]) ? 0 : 1,
            (string)(shell["version"] ?? ""),
            Convert.ToInt32(((Dictionary<string, object?>)shell["totals"]!)["articles"]),
            PhpPlanQ1Hint.PhpEmpty(cachedStatus["ok"]) ? 0 : 1,
            (string)(cachedStatus["error"] ?? ""),
            mon,
            PhpPlanQ1Hint.PathEndsWith(PhpPlanQ1Hint.EpcCataDefaultCategoriesJsonPath(), "/content/general_pages/epc_cata_default_categories.json") ? 1 : 0,
            PhpPlanQ1Hint.PathEndsWith(PhpPlanQ1Hint.EpcCataDefaultSubcategoriesJsonPath(), "/content/general_pages/epc_cata_default_subcategories.json") ? 1 : 0,
            PhpPlanQ1Hint.PathEndsWith(PhpPlanQ1Hint.EpcCataCategoryIconDir(), "/content/files/epc-cata/category-icons/") ? 1 : 0,
            PhpPlanQ1Hint.EpcCataCategoryIconsAvailable().Count,
            PhpPlanQ1Hint.EpcCataCategoryConfigRaw() is null ? 1 : 0,
            PhpPlanQ1Hint.EpcCataCategoryConfigReset() ? 1 : 0,
            PhpPlanQ1Hint.EpcCataCategoryConfigSaveBulk([]) ? 1 : 0,
            PhpPlanQ1Hint.EpcCataCategoryConfigSaveOne(new Dictionary<string, object?>(StringComparer.Ordinal) { ["STR_ID"] = 2 }) ? 1 : 0,
            new List<object>()
        };
    }

    private static object Tenant()
    {
        PhpPlanQ1Hint.LangHref = "";
        var none = PhpPlanQ1Hint.EpcCataStorefrontUrl();
        PhpPlanQ1Hint.LangHref = "/ar/";
        var ar = PhpPlanQ1Hint.EpcCataStorefrontUrl();
        PhpPlanQ1Hint.LangHref = "/en";
        var en = PhpPlanQ1Hint.EpcCataStorefrontUrl();
        PhpPlanQ1Hint.LangHref = "/ar/";
        var stillAr = PhpPlanQ1Hint.EpcCataStorefrontUrl();
        PhpPlanQ1Hint.LangHref = "/en/";
        var backEn = PhpPlanQ1Hint.EpcCataStorefrontUrl();
        PhpPlanQ1Hint.FileConfigValues = new Dictionary<string, object?>(StringComparer.Ordinal) { ["enabled"] = 0 };
        PhpPlanQ1Hint.AutoPartsSite = true;
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "shop.example";
        var disabled = PhpPlanQ1Hint.EpcCataEnabledForRequest() ? 1 : 0;
        var file = PhpPlanQ1Hint.EpcCataFileConfig();
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.epartscart.com";
        var epartsStill = PhpPlanQ1Hint.EpcCataEnabledForRequest() ? 1 : 0;
        return new object[]
        {
            none,
            ar,
            en,
            stillAr,
            backEn,
            none == ar ? 1 : 0,
            ar == en ? 1 : 0,
            stillAr == ar ? 1 : 0,
            disabled,
            file.ContainsKey("enabled") ? Convert.ToInt32(file["enabled"]) : -1,
            epartsStill
        };
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static string FirstDiff(string expected, string actual)
    {
        var n = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < n; i++)
        {
            if (expected[i] != actual[i])
            {
                var from = Math.Max(0, i - 40);
                return "at " + i + " exp=" + expected[from..Math.Min(expected.Length, i + 80)] + " got=" + actual[from..Math.Min(actual.Length, i + 80)];
            }
        }

        return expected.Length == actual.Length
            ? "equal-len-mismatch"
            : "len exp=" + expected.Length + " got=" + actual.Length + " tailExp=" + expected[Math.Max(0, expected.Length - 80)..] + " tailGot=" + actual[Math.Max(0, actual.Length - 80)..];
    }

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
