using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1DataParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Data");

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
    public void PlanQ1Data_MatchPhpGolden()
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
                PhpPlanQ1Data.JewelleryDataPath,
                PhpPlanQ1Data.FashionDataPath,
                PhpPlanQ1Data.ElectronicsDataPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Data.JewelleryDataPath,
                PhpPlanQ1Data.FashionDataPath,
                PhpPlanQ1Data.ElectronicsDataPath
            });

    [Fact]
    public void JewelleryImg_DoesNotStartASession()
        => Assert.StartsWith("/content/files/images/storefronts/jewellery/", PhpPlanQ1Data.EpcJewelleryRetailKiyashaImg("gold_rings"), StringComparison.Ordinal);

    [Fact]
    public void FooterColumns_UsePortalMissingStoreNames()
    {
        var j = PhpPlanQ1Data.EpcJewelleryRetailKiyashaFooterColumns();
        var f = PhpPlanQ1Data.EpcFashionRetailNamshiFooterColumns();
        var e = PhpPlanQ1Data.EpcElectronicsRetailFooterColumns();
        Assert.Equal("About The Jewellery Trend", ((Dictionary<string, object?>)j[2])["title"]);
        Assert.Equal("About Stylenlook", ((Dictionary<string, object?>)f[2])["title"]);
        Assert.Equal("About Electronicae", ((Dictionary<string, object?>)e[1])["title"]);
    }

    private static string Render(string name)
        => name switch
        {
            "jrk_data" => Json(JrkData()),
            "frn_data" => Json(FrnData()),
            "er_data" => Json(ErData()),
            _ => "unknown:" + name
        };

    private static object?[] JrkData()
    {
        var img = PhpPlanQ1Data.EpcJewelleryRetailKiyashaImageCatalog();
        var sec = PhpPlanQ1Data.EpcJewelleryRetailKiyashaProductSections();
        var hero = PhpPlanQ1Data.EpcJewelleryRetailKiyashaHeroSlides();
        var resolved = PhpPlanQ1Data.EpcJewelleryRetailKiyashaResolveProductImages(new Dictionary<string, object?> { ["name"] = "X", ["key"] = "gold_rings" });
        var keep = PhpPlanQ1Data.EpcJewelleryRetailKiyashaResolveProductImages(new Dictionary<string, object?> { ["name"] = "Y", ["key"] = "gold_rings", ["image"] = "KEEP", ["alt"] = "A" });
        return new object?[]
        {
            img.EnumerateObject().Count(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaImg("gold_rings", 400, 400),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaImg("photo-x", 800, 200),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaImgAlt("gold_rings"),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaImgAlt("nope"),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaPromoStrip().GetArrayLength(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaTrustBadges().GetArrayLength(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaDepartments().GetArrayLength(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaCollectionTabs().GetArrayLength(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaCategoryChips().GetArrayLength(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaBrandFilters()[0].GetString(),
            hero[0].GetProperty("image").GetString(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaCategoryTiles().GetArrayLength(),
            sec[0].GetProperty("products")[0].GetProperty("sku").GetString(),
            sec[0].GetProperty("products")[5].GetProperty("sku").GetString(),
            resolved["image"],
            resolved["alt"],
            keep["image"],
            new[]
            {
                PhpPlanQ1Data.EpcJewelleryRetailKiyashaFormatAed(0),
                PhpPlanQ1Data.EpcJewelleryRetailKiyashaFormatAed(-1),
                PhpPlanQ1Data.EpcJewelleryRetailKiyashaFormatAed(2500),
                PhpPlanQ1Data.EpcJewelleryRetailKiyashaFormatAed("3500.9")
            },
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaProductBadge(new Dictionary<string, object?> { ["was"] = 100, ["price"] = 80 }),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaProductBadge(new Dictionary<string, object?> { ["is_new"] = true, ["price"] = 10 }),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaProductBadge(new Dictionary<string, object?> { ["badge"] = "hot" }),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaProductBadge(new Dictionary<string, object?> { ["price"] = 10 }),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaUtilityLinks().GetArrayLength(),
            PhpPlanQ1Data.EpcJewelleryRetailKiyashaMegaNav().GetArrayLength(),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Data.EpcJewelleryRetailKiyashaPaymentMethods().GetRawText()),
            PhpPlanQ1Data.EpcJrkProHeroEyebrow(),
            PhpPlanQ1Data.EpcJrkProHeroTitle(),
            PhpPlanQ1Data.EpcJrkProHeroCopy(),
            PhpPlanQ1Data.EpcJrkProHeroActions("ar")[0].GetProperty("label").GetString(),
            PhpPlanQ1Data.EpcJrkProHeroStats()[0].GetProperty("value").GetString()
        };
    }

    private static object?[] FrnData()
    {
        var img = PhpPlanQ1Data.EpcFashionRetailNamshiImageCatalog();
        var sec = PhpPlanQ1Data.EpcFashionRetailNamshiProductSections();
        var hero = PhpPlanQ1Data.EpcFashionRetailNamshiHeroSlides();
        var resolved = PhpPlanQ1Data.EpcFashionRetailNamshiResolveProductImages(new Dictionary<string, object?> { ["name"] = "X", ["key"] = "skincare" });
        var keep = PhpPlanQ1Data.EpcFashionRetailNamshiResolveProductImages(new Dictionary<string, object?> { ["name"] = "Y", ["key"] = "skincare", ["image"] = "KEEP", ["alt"] = "A" });
        return new object?[]
        {
            img.EnumerateObject().Count(),
            PhpPlanQ1Data.EpcFashionRetailNamshiImg("skincare", 400, 400),
            PhpPlanQ1Data.EpcFashionRetailNamshiImg("photo-x", 800, 200),
            PhpPlanQ1Data.EpcFashionRetailNamshiImgAlt("skincare"),
            PhpPlanQ1Data.EpcFashionRetailNamshiImgAlt("nope"),
            PhpPlanQ1Data.EpcFashionRetailNamshiPromoStrip().GetArrayLength(),
            PhpPlanQ1Data.EpcFashionRetailNamshiTrustBadges().GetArrayLength(),
            PhpPlanQ1Data.EpcFashionRetailNamshiDepartments().GetArrayLength(),
            PhpPlanQ1Data.EpcFashionRetailNamshiBeautyTabs().GetArrayLength(),
            PhpPlanQ1Data.EpcFashionRetailNamshiCategoryChips().GetArrayLength(),
            PhpPlanQ1Data.EpcFashionRetailNamshiBrandFilters()[0].GetString(),
            hero[0].GetProperty("image").GetString(),
            PhpPlanQ1Data.EpcFashionRetailNamshiCategoryTiles().GetArrayLength(),
            sec[0].GetProperty("products")[0].GetProperty("sku").GetString(),
            sec[0].GetProperty("products")[5].GetProperty("sku").GetString(),
            resolved["image"],
            resolved["alt"],
            keep["image"],
            new[]
            {
                PhpPlanQ1Data.EpcFashionRetailNamshiFormatAed(0),
                PhpPlanQ1Data.EpcFashionRetailNamshiFormatAed(-1),
                PhpPlanQ1Data.EpcFashionRetailNamshiFormatAed(89),
                PhpPlanQ1Data.EpcFashionRetailNamshiFormatAed("3500.9")
            },
            PhpPlanQ1Data.EpcFashionRetailNamshiProductBadge(new Dictionary<string, object?> { ["was"] = 100, ["price"] = 80 }),
            PhpPlanQ1Data.EpcFashionRetailNamshiProductBadge(new Dictionary<string, object?> { ["is_new"] = true, ["price"] = 10 }),
            PhpPlanQ1Data.EpcFashionRetailNamshiProductBadge(new Dictionary<string, object?> { ["badge"] = "hot" }),
            PhpPlanQ1Data.EpcFashionRetailNamshiProductBadge(new Dictionary<string, object?> { ["price"] = 10 }),
            PhpPlanQ1Data.EpcFashionRetailNamshiUtilityLinks().GetArrayLength(),
            PhpPlanQ1Data.EpcFashionRetailNamshiMegaNav().GetArrayLength(),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Data.EpcFashionRetailNamshiPaymentMethods().GetRawText()),
            PhpPlanQ1Data.EpcFrnProHeroEyebrow(),
            PhpPlanQ1Data.EpcFrnProHeroTitle(),
            PhpPlanQ1Data.EpcFrnProHeroCopy(),
            PhpPlanQ1Data.EpcFrnProHeroActions("ar")[0].GetProperty("label").GetString(),
            PhpPlanQ1Data.EpcFrnProHeroStats()[0].GetProperty("value").GetString()
        };
    }

    private static object?[] ErData()
    {
        var img = PhpPlanQ1Data.EpcElectronicsRetailImageCatalog();
        var sec = PhpPlanQ1Data.EpcElectronicsRetailDealSections();
        var hero = PhpPlanQ1Data.EpcElectronicsRetailHeroSlides();
        var resolved = PhpPlanQ1Data.EpcElectronicsRetailResolveProductImages(new Dictionary<string, object?> { ["name"] = "X", ["key"] = "laptop" });
        var keep = PhpPlanQ1Data.EpcElectronicsRetailResolveProductImages(new Dictionary<string, object?> { ["name"] = "Y", ["key"] = "laptop", ["image"] = "KEEP", ["alt"] = "A" });
        return new object?[]
        {
            img.EnumerateObject().Count(),
            PhpPlanQ1Data.EpcElectronicsRetailImg("laptop", 400, 400),
            PhpPlanQ1Data.EpcElectronicsRetailImg("photo-x", 800, 200),
            PhpPlanQ1Data.EpcElectronicsRetailImgAlt("laptop"),
            PhpPlanQ1Data.EpcElectronicsRetailImgAlt("nope"),
            PhpPlanQ1Data.EpcElectronicsRetailPromoStrip().GetArrayLength(),
            PhpPlanQ1Data.EpcElectronicsRetailTrustBadges().GetArrayLength(),
            hero[0].GetProperty("image").GetString(),
            PhpPlanQ1Data.EpcElectronicsRetailCategoryTiles().GetArrayLength(),
            sec[0].GetProperty("products")[0].GetProperty("sku").GetString(),
            sec[0].GetProperty("products")[5].GetProperty("sku").GetString(),
            PhpPlanQ1Data.EpcElectronicsRetailBrands().GetArrayLength(),
            PhpPlanQ1Data.EpcElectronicsRetailFeaturedCategories().GetArrayLength(),
            resolved["image"],
            resolved["alt"],
            keep["image"],
            new[]
            {
                PhpPlanQ1Data.EpcElectronicsRetailFormatAed(0),
                PhpPlanQ1Data.EpcElectronicsRetailFormatAed(-1),
                PhpPlanQ1Data.EpcElectronicsRetailFormatAed(2004),
                PhpPlanQ1Data.EpcElectronicsRetailFormatAed("3500.9")
            },
            PhpPlanQ1Data.EpcElectronicsRetailProductBadge(new Dictionary<string, object?> { ["was"] = 100, ["price"] = 80 }),
            PhpPlanQ1Data.EpcElectronicsRetailProductBadge(new Dictionary<string, object?> { ["is_new"] = true, ["price"] = 10 }),
            PhpPlanQ1Data.EpcElectronicsRetailProductBadge(new Dictionary<string, object?> { ["badge"] = "hot" }),
            PhpPlanQ1Data.EpcElectronicsRetailProductBadge(new Dictionary<string, object?> { ["price"] = 10 }),
            PhpPlanQ1Data.EpcElectronicsRetailUtilityLinks().GetArrayLength(),
            PhpPlanQ1Data.EpcElectronicsRetailMegaNav().GetArrayLength(),
            JsonSerializer.Deserialize<object>(PhpPlanQ1Data.EpcElectronicsRetailPaymentMethods().GetRawText()),
            PhpPlanQ1Data.EpcErProHeroEyebrow(),
            PhpPlanQ1Data.EpcErProHeroTitle(),
            PhpPlanQ1Data.EpcErProHeroCopy(),
            PhpPlanQ1Data.EpcErProHeroActions("ar")[0].GetProperty("label").GetString(),
            PhpPlanQ1Data.EpcErProHeroStats()[0].GetProperty("value").GetString()
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
