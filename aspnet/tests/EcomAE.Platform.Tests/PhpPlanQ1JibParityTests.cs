using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1JibParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Jib");

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
    public void PlanQ1Jib_MatchPhpGolden()
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
                using var left = JsonDocument.Parse(Json(actual.Extra));
                failures.Add(name + " " + FirstValueDiff(left.RootElement, expected, name));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Jib.TenantShowcasePath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Jib.TenantShowcasePath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Jib.Reset();
        Assert.Contains("epc_ecomae_platform_tenant_showcase.php", PhpPlanQ1Jib.TenantShowcasePath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Jib.TenantShowcasePath, StringComparison.Ordinal);
        var shot = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("Acme-1!");
        Assert.DoesNotContain("PHPSESSID", shot, StringComparison.Ordinal);
        var html = PhpPlanQ1Jib.EpcEcomaePlatformTenantAnimatedLogo("fashion", "O'Name");
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        Assert.Contains("&#039;", html, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Jib.Reset();
        return name switch
        {
            "themes" => Themes(),
            "rows" => Rows(),
            "shots" => Shots(),
            "html" => Html(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Themes()
    {
        var themes = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseThemes();
        var codes = new[]
        {
            "auto_parts", "AUTO_PARTS", "Acme-1!", "jewellery", "nope", "fashion", "electronics", "tax_advisory", ""
        };
        var picked = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var mods = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            picked[code] = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseTheme(code);
            keys[code] = PhpPlanQ1Jib.EpcEcomaePlatformTenantKeyForIndustry(code);
            mods[code] = PhpPlanQ1Jib.EpcEcomaePlatformTenantCpModules(code);
        }

        var css = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseStyles();
        return new Rendered(new object?[]
        {
            themes.Keys.ToList(),
            themes["jewellery"],
            picked,
            keys,
            mods,
            css.Length,
            css[..Math.Min(80, css.Length)],
            css.Length <= 80 ? css : css[^80..]
        });
    }

    private static Rendered Rows()
    {
        PhpPlanQ1Jib.CustomerResults = TenantsDefault;
        var rows = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseRows();
        var slim = rows.Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = Str(row, "key"),
            ["industry"] = Str(row, "industry"),
            ["theme"] = ThemeField(row, "theme"),
            ["label"] = ThemeField(row, "label"),
            ["name"] = Str(row, "name")
        }).ToList();
        PhpPlanQ1Jib.CustomerResults = () => [];
        var empty = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseRows();
        PhpPlanQ1Jib.CustomerResults = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = "stylenlook",
                ["name"] = "Style",
                ["industry"] = "Fashion!"
            }
        ];
        var mapped = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseRows();
        return new Rendered(new object?[]
        {
            slim,
            empty,
            Str(mapped[0], "industry"),
            ThemeField(mapped[0], "hero_type")
        });
    }

    private static Rendered Shots()
    {
        var shots = new Dictionary<string, string>(StringComparer.Ordinal);
        PhpPlanQ1Jib.Screenshot = slug => shots.TryGetValue(slug, out var path) ? path : "";
        var miss = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("");
        var blank = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("!!!");
        var none = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("epartscart");
        shots["tenant-epartscart-storefront"] = "/content/files/images/tenant-epartscart-storefront.webp";
        var webp = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("Eparts-Cart!");
        shots["tenant-electronicae-storefront"] = "/content/files/images/tenant-electronicae-storefront.PNG";
        var png = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontScreenshot("electronicae");
        var previewLive = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontPreview("auto_parts", "Storefront", "epartscart");
        var previewAnim = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontPreview("jewellery", "Store o'front", "");
        var previewKey = PhpPlanQ1Jib.EpcEcomaePlatformTenantStorefrontPreview("electronics");
        return new Rendered(new object?[] { miss, blank, none, webp, png, previewLive, previewAnim, previewKey });
    }

    private static Rendered Html()
    {
        var shots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant-epartscart-storefront"] = "/content/files/images/tenant-epartscart-storefront.png"
        };
        PhpPlanQ1Jib.CustomerResults = TenantsDefault;
        PhpPlanQ1Jib.Screenshot = slug => shots.TryGetValue(slug, out var path) ? path : "";
        PhpPlanQ1Jib.BaseUrl = () => "https://www.ecomae.com/";
        var logos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var code in new[] { "auto_parts", "tax_advisory", "electronics", "fashion", "jewellery", "nope" })
        {
            logos[code] = PhpPlanQ1Jib.EpcEcomaePlatformTenantAnimatedLogo(code, code == "fashion" ? "O'Name" : "");
        }

        var heroes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var code in new[] { "auto_parts", "tax_advisory", "electronics", "fashion", "jewellery" })
        {
            heroes[code] = PhpPlanQ1Jib.EpcEcomaePlatformTenantMiniHeroVisual(code);
        }

        var cp = PhpPlanQ1Jib.EpcEcomaePlatformTenantCpPreview("jewellery", "CP o'panel");
        var cpDefault = PhpPlanQ1Jib.EpcEcomaePlatformTenantCpPreview("nope");
        var card = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseCard(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = "epartscart",
            ["name"] = "O'Parts",
            ["industry"] = "auto_parts",
            ["outcome"] = "Flagship o'biz",
            ["site_url"] = "https://www.epartscart.com/",
            ["portal_url"] = "https://www.epartscart.com/cp/",
            ["logo_url"] = "/logos/x.png"
        });
        var cardNoLogo = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseCard(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = "taxofinca",
            ["name"] = "taxofinca",
            ["industry"] = "tax_advisory",
            ["outcome"] = "Advisory",
            ["site_url"] = "#",
            ["portal_url"] = "#",
            ["logo_url"] = ""
        });
        var page = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseSection("page");
        var home = PhpPlanQ1Jib.EpcEcomaePlatformTenantShowcaseSection("home");
        var themedLive = PhpPlanQ1Jib.EpcEcomaePlatformIndustryThemedPreviews("auto_parts", "Auto parts");
        var themedAnim = PhpPlanQ1Jib.EpcEcomaePlatformIndustryThemedPreviews("fashion", "Fashion o'shop");
        return new Rendered(new object?[] { logos, heroes, cp, cpDefault, card, cardNoLogo, page, home, themedLive, themedAnim });
    }

    private static List<Dictionary<string, object?>> TenantsDefault()
        =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = "epartscart",
                ["name"] = "eParts Cart",
                ["industry"] = "auto_parts",
                ["outcome"] = "Flagship",
                ["site_url"] = "https://www.epartscart.com/",
                ["portal_url"] = "https://www.epartscart.com/cp/",
                ["logo_url"] = "/logos/epartscart.png"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = "taxofinca",
                ["name"] = "O'Brien tax",
                ["outcome"] = "Advisory",
                ["site_url"] = "https://www.taxofinca.com/",
                ["portal_url"] = "https://www.taxofinca.com/cp/",
                ["logo_url"] = ""
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = "unknown_tn",
                ["name"] = "Ghost",
                ["outcome"] = ""
            }
        ];

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var raw) && raw != null ? Convert.ToString(raw) ?? "" : "";

    private static string ThemeField(Dictionary<string, object?> row, string key)
    {
        if (row.TryGetValue("theme_meta", out var meta) && meta is Dictionary<string, string> theme
            && theme.TryGetValue(key, out var value))
        {
            return value;
        }

        return "";
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

    private static string FirstValueDiff(JsonElement left, JsonElement right, string path)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return path + " kind " + left.ValueKind + " vs " + right.ValueKind;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in right.EnumerateObject())
                {
                    if (!left.TryGetProperty(prop.Name, out var other))
                    {
                        return path + "." + prop.Name + " missing on actual";
                    }

                    var nested = FirstValueDiff(other, prop.Value, path + "." + prop.Name);
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out _))
                    {
                        return path + "." + prop.Name + " extra on actual";
                    }
                }

                return "";
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                if (a.Count != b.Count)
                {
                    return path + " len " + a.Count + " vs " + b.Count;
                }

                for (var i = 0; i < a.Count; i++)
                {
                    var nested = FirstValueDiff(a[i], b[i], path + "[" + i + "]");
                    if (nested != "")
                    {
                        return nested;
                    }
                }

                return "";
            case JsonValueKind.String:
                var ls = left.GetString() ?? "";
                var rs = right.GetString() ?? "";
                if (ls == rs)
                {
                    return "";
                }

                var n = Math.Min(ls.Length, rs.Length);
                var at = 0;
                while (at < n && ls[at] == rs[at])
                {
                    at++;
                }

                static string Clip(string value, int at)
                {
                    var start = Math.Max(0, at - 70);
                    var len = Math.Min(140, value.Length - start);
                    return value.Substring(start, len).Replace("\n", "\\n").Replace("\t", "\\t");
                }

                return path + " at=" + at + " expLen=" + rs.Length + " gotLen=" + ls.Length
                    + " exp=" + Clip(rs, at) + " got=" + Clip(ls, at);
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble()
                    ? ""
                    : path + " num " + left.GetRawText() + " vs " + right.GetRawText();
            default:
                return "";
        }
    }
}
