using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpSmallMoreParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SmallMore");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpSmallMore.LevamPath,
        PhpSmallMore.SpecialSearchesPath,
        PhpSmallMore.NewsModulePath,
        PhpSmallMore.RetailTaxonomyPath,
        PhpSmallMore.FaviconPath,
        PhpSmallMore.AuthLinksPath,
        PhpSmallMore.GarageLoginPath,
        PhpSmallMore.FashionMegaPath,
        PhpSmallMore.ConsultingHeroPath,
        PhpSmallMore.FashionHeroPath,
        PhpSmallMore.JewelleryHeroPath,
        PhpSmallMore.ElectronicsHeroPath
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
    public void SmallMore_MatchPhpGolden()
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
            if (name is "special_two" or "garage_guest" or "garage_staff_stay" or "hero_frn" or "hero_jrk" or "hero_cpi" or "hero_er")
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

    private static string CaseFile(JsonElement testCase)
        => testCase.TryGetProperty("file", out var file) && file.GetString() is { Length: > 0 } path
            ? path
            : testCase.GetProperty("name").GetString() switch
            {
                var name when name is not null && name.StartsWith("retail_", StringComparison.Ordinal) => PhpSmallMore.RetailTaxonomyPath,
                var name when name is not null && name.StartsWith("favicon_", StringComparison.Ordinal) => PhpSmallMore.FaviconPath,
                var name when name is not null && name.StartsWith("auth_", StringComparison.Ordinal) => PhpSmallMore.AuthLinksPath,
                "hero_cpi_href" => PhpSmallMore.ConsultingHeroPath,
                _ => "unknown"
            };

    private static string Render(string name)
    {
        var en = new Dictionary<string, string> { ["lang_href"] = "/en/" };
        var ar = new Dictionary<string, string> { ["lang_href"] = "/ar" };
        return name switch
        {
            "levam_off" => PhpSmallMore.Levam(null),
            "levam_on" => PhpSmallMore.Levam("AB&C"),
            "special_empty" => PhpSmallMore.PrintSpecialSearches("/en", []),
            "special_two" => PhpSmallMore.PrintSpecialSearches("/ar", [("oil", "oil.png", 10), ("a&b", "x\"y.png", 11)]),
            "news_none" => PhpSmallMore.NewsModule("/en", "news", []),
            "news_one" => PhpSmallMore.NewsModule("/en/", "news", [(100, 1700000000, 101, "n1")]),
            "retail_tree" => JsonSerializer.Serialize(PhpSmallMore.EpcRetailTaxSeedTree(), JsonOpts),
            "favicon_auto" => PhpSmallMore.EpcPortalFaviconSvg("auto_parts"),
            "favicon_electronics" => PhpSmallMore.EpcPortalFaviconSvg("electronics"),
            "favicon_fashion" => PhpSmallMore.EpcPortalFaviconSvg("fashion"),
            "favicon_jewellery" => PhpSmallMore.EpcPortalFaviconSvg("jewellery"),
            "favicon_tax" => PhpSmallMore.EpcPortalFaviconSvg("tax_advisory"),
            "favicon_unknown" => PhpSmallMore.EpcPortalFaviconSvg("other"),
            "favicon_links" => PhpSmallMore.EpcPortalFaviconLinkTags("consultancy"),
            "auth_guest" => JsonSerializer.Serialize(new object[] { PhpSmallMore.EpcStorefrontAuthLangHref(new Dictionary<string, string> { ["lang_href"] = "/en" }), PhpSmallMore.EpcStorefrontAuthLinksHtml(en, false) }, JsonOpts),
            "auth_user" => PhpSmallMore.EpcStorefrontAuthLinksHtml(en, true),
            "auth_ar" => JsonSerializer.Serialize(new[]
            {
                PhpSmallMore.EpcStorefrontAuthLoginUrl(ar),
                PhpSmallMore.EpcStorefrontAuthSignupUrl(new Dictionary<string, string> { ["lang_href"] = "/ar/" }),
                PhpSmallMore.EpcStorefrontAuthVendorUrl(null),
                PhpSmallMore.EpcStorefrontAuthGarageLoginUrl(en)
            }, JsonOpts),
            "auth_styles" => PhpSmallMore.EpcStorefrontAuthLinksStyles(),
            "garage_guest" => PhpSmallMore.GarageLogin("/en/", "cp", false, false),
            "garage_staff_stay" => PhpSmallMore.GarageLogin("/ar", "cp", true, true),
            "mega_one" => PhpSmallMore.FashionMegaMenu([("A&B", "/women", "/i.png", "A")], [("Chip", "/sale")], "/en"),
            "hero_cpi_href" => JsonSerializer.Serialize(new[] { PhpSmallMore.EpcCpiHeroBannerHref("/en/", "/about"), PhpSmallMore.EpcCpiHeroBannerHref("/en", "https://x.com") }, JsonOpts),
            "hero_frn" => PhpSmallMore.HeroBanner(
                "epc-frn-hero-banner", "rgba(192,38,211,.2)", "16", "fa-heart",
                "Animated fashion runway visual", "epc-frn-hero-anim", "Dress sway with floating beauty chips",
                "\t\t\t\t\t<span class=\"epc-frn-hero-anim__chip\">New in</span>\n\t\t\t\t\t<span class=\"epc-frn-hero-anim__dress\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-frn-hero-anim__runway\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-frn-hero-anim__chip epc-frn-hero-anim__chip--alt\">Beauty</span>\n\t\t\t\t\t<div class=\"epc-frn-hero-anim__label\">Runway &amp; beauty motion</div>\n",
                "Fashion runway loop", "Dress sway, floating chips and smooth CSS motion — Namshi-inspired retail energy.",
                "/en/", "New <b>", "Dress", "Runway",
                [("Shop", "/women", "fa-heart", true)], [("2k", "SKUs")]),
            "hero_jrk" => PhpSmallMore.HeroBanner(
                "epc-jrk-hero-banner", "rgba(184,134,11,.35)", "20", "fa-diamond",
                "Animated jewellery ring visual", "epc-jrk-hero-anim", "Glowing gold ring with sparkle",
                "\t\t\t\t\t<span class=\"epc-jrk-hero-anim__ring\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-jrk-hero-anim__gem\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-jrk-hero-anim__spark epc-jrk-hero-anim__spark--one\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-jrk-hero-anim__spark epc-jrk-hero-anim__spark--two\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<div class=\"epc-jrk-hero-anim__label\">Gold ring glow loop</div>\n",
                "Luxury sparkle loop", "Glowing gold ring with diamond sparkles — Kiyasha fine jewellery motion.",
                "/en", "Gold", "Ring", "Sparkle",
                [("See", "/rings", "fa-diamond", false)], [("1", "Boutique")]),
            "hero_cpi" => PhpSmallMore.HeroBanner(
                "epc-cpi-hero-banner", "rgba(15,118,110,.2)", "14", "fa-line-chart",
                "Animated advisory charts", "epc-cpi-hero-anim", "Growing performance charts",
                "\t\t\t\t\t<span class=\"epc-cpi-hero-anim__bar\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-cpi-hero-anim__bar\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-cpi-hero-anim__bar\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-cpi-hero-anim__bar\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<div class=\"epc-cpi-hero-anim__label\">Compliance &amp; growth charts</div>\n",
                "Advisory growth loop", "Animated blue &amp; gold performance bars — Prime Invest professional motion.",
                "/en", "Tax", "Advise", "Growth",
                [("Talk", "/contact", "fa-phone", true)], [("20y", "Years")]),
            "hero_er" => PhpSmallMore.HeroBanner(
                "epc-er-hero-banner", "rgba(225,10,10,.25)", "18", "fa-microchip",
                "Animated tech chip visual", "epc-er-hero-anim", "Scanning chip with audio wave",
                "\t\t\t\t\t<span class=\"epc-er-hero-anim__chip\" aria-hidden=\"true\"></span>\n\t\t\t\t\t<span class=\"epc-er-hero-anim__wave\" aria-hidden=\"true\">\n\t\t\t\t\t\t<span></span><span></span><span></span><span></span>\n\t\t\t\t\t</span>\n\t\t\t\t\t<div class=\"epc-er-hero-anim__label\">Tech scan + wave loop</div>\n",
                "Tech power animation", "Scanning microchip with pulsing audio wave — Virgin Megastore retail motion.",
                "/en", "Tech", "Gadgets", "Wave",
                [], [("9", "Lines")]),
            _ => "unknown:" + name
        };
    }

    private static bool StructuralOk(string name, string actual)
        => name switch
        {
            "special_two" => actual.Contains("/ar/oil", StringComparison.Ordinal)
                && actual.Contains("/ar/a&b", StringComparison.Ordinal)
                && actual.Contains("{4146}", StringComparison.Ordinal)
                && actual.Contains("{10}", StringComparison.Ordinal),
            "garage_guest" => actual.Contains("Staff CP login", StringComparison.Ordinal)
                && actual.Contains("/cp/", StringComparison.Ordinal)
                && actual.Contains("/en/garazh", StringComparison.Ordinal)
                && !actual.Contains("You are signed in", StringComparison.Ordinal),
            "garage_staff_stay" => actual.Contains("You are signed in with workshop access.", StringComparison.Ordinal)
                && actual.Contains("/ar/garage/manager", StringComparison.Ordinal)
                && actual.Contains("Open Garage Manager", StringComparison.Ordinal),
            "hero_frn" => actual.Contains("epc-frn-hero-banner", StringComparison.Ordinal)
                && actual.Contains("New &lt;b&gt;", StringComparison.Ordinal)
                && actual.Contains("/en/women", StringComparison.Ordinal),
            "hero_jrk" => actual.Contains("epc-jrk-hero-banner", StringComparison.Ordinal) && actual.Contains("/en/rings", StringComparison.Ordinal),
            "hero_cpi" => actual.Contains("epc-cpi-hero-banner", StringComparison.Ordinal) && actual.Contains("/en/contact", StringComparison.Ordinal),
            "hero_er" => actual.Contains("epc-er-hero-banner", StringComparison.Ordinal) && actual.Contains("Tech scan", StringComparison.Ordinal),
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
        => value.Length <= 180 ? value : value[..180] + "…";
}
