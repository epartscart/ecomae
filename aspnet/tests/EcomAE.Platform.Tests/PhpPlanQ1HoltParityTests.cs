using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HoltParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Holt");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Holt_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_epartscart_accessories.php", PhpPlanQ1Holt.AccessoriesPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Holt.Reset();
        var html = PhpPlanQ1Holt.RenderPage("/en");
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Holt.AccessoriesPath, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-accessories\"", html, StringComparison.Ordinal);
        Assert.Contains("data-lang=\"/en\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Area_AccessoriesLangIsolation()
    {
        PhpPlanQ1Holt.Reset();
        var ar = PhpPlanQ1Holt.RenderPage("/ar/");
        var en = PhpPlanQ1Holt.RenderPage("/en");
        Assert.Contains("data-lang=\"/ar\"", ar, StringComparison.Ordinal);
        Assert.Contains("data-lang=\"/en\"", en, StringComparison.Ordinal);
        Assert.DoesNotContain("data-lang=\"/en\"", ar, StringComparison.Ordinal);
        Assert.DoesNotContain("data-lang=\"/ar\"", en, StringComparison.Ordinal);
        var item = new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = "7", ["category"] = "brakes" };
        var arHref = PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/ar/"));
        var enHref = PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/en"));
        Assert.StartsWith("/ar/accessories-spare-parts", arHref, StringComparison.Ordinal);
        Assert.StartsWith("/en/accessories-spare-parts", enHref, StringComparison.Ordinal);
        Assert.DoesNotContain("/en/", arHref, StringComparison.Ordinal);
        Assert.DoesNotContain("/ar/", enHref, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Holt.Reset();
        return name switch
        {
            "empty" => Empty(),
            "page" => Page(),
            "live" => Live(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var on = PhpPlanQ1Holt.ToggleSaved("42") ? 1 : 0;
        var off = PhpPlanQ1Holt.ToggleSaved("42") ? 1 : 0;
        var bar = PhpPlanQ1Holt.UpdateSavedBar();
        var item = Map(("id", "99"), ("category", "brakes"), ("subcategory", "pads"));
        var photos = PhpPlanQ1Holt.ListingPhotos(Map(
            ("photos", new List<object?> { " /a.jpg ", Map(("url", "https://cdn/b.jpg")), Map(("url", "")) }),
            ("image_url", "https://cdn/fallback.jpg")));
        var fallback = PhpPlanQ1Holt.ListingPhotos(Map(("image_url", "https://cdn/only.jpg")));
        var state = Map(
            ("q", "oil"), ("category", "car-care"), ("subcategory", ""), ("make", "Toyota"),
            ("model", ""), ("city", ""), ("condition", ""), ("price_min", ""), ("price_max", ""),
            ("id", ""), ("sort", "updated-desc"), ("page", 2));
        var read = PhpPlanQ1Holt.ReadUrl("/en/accessories-spare-parts?q=oil&category=car-care&make=Toyota&page=2");
        var rows = new List<Dictionary<string, object?>>
        {
            Map(("slug", "brakes"), ("label", "Brakes"), ("count", 4)),
            Map(("slug", "interior"), ("label", "Interior"), ("count", 2))
        };
        return new object[]
        {
            PhpPlanQ1Holt.Text(null),
            PhpPlanQ1Holt.Text(0),
            PhpPlanQ1Holt.Esc("A & B <x> \"q\" 'z'"),
            PhpPlanQ1Holt.Money(0, "AED"),
            PhpPlanQ1Holt.Money(-5, "AED"),
            PhpPlanQ1Holt.Money(1234, ""),
            PhpPlanQ1Holt.Money("abc", "AED"),
            PhpPlanQ1Holt.PageBase("/en"),
            PhpPlanQ1Holt.PageBase("/ar/"),
            PhpPlanQ1Holt.RelativeUpdated(0),
            PhpPlanQ1Holt.RelativeUpdated(PhpPlanQ1Holt.NowUnix - 30),
            PhpPlanQ1Holt.RelativeUpdated(PhpPlanQ1Holt.NowUnix - 120),
            PhpPlanQ1Holt.RelativeUpdated(PhpPlanQ1Holt.NowUnix - 7200),
            PhpPlanQ1Holt.RelativeUpdated(PhpPlanQ1Holt.NowUnix - 172800),
            PhpPlanQ1Holt.RelativeUpdated(PhpPlanQ1Holt.NowUnix - 700000),
            PhpPlanQ1Holt.ListingHref(null, PhpPlanQ1Holt.PageBase("/en")),
            PhpPlanQ1Holt.ListingHref(Map(("id", "")), PhpPlanQ1Holt.PageBase("/en")),
            PhpPlanQ1Holt.ListingHref(Map(("id", "7"), ("detail_url", "/en/accessories-spare-parts?id=7")), PhpPlanQ1Holt.PageBase("/en")),
            PhpPlanQ1Holt.ListingHref(Map(("id", "7"), ("url", "/old?id=7")), PhpPlanQ1Holt.PageBase("/en")),
            PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/en")),
            photos.Count,
            photos.Count > 1 ? photos[1] : "",
            fallback.Count,
            fallback.Count > 0 ? fallback[0] : "",
            PhpPlanQ1Holt.BrowseBackHref(new Dictionary<string, object?>(StringComparer.Ordinal), PhpPlanQ1Holt.PageBase("/en")),
            PhpPlanQ1Holt.BrowseBackHref(item, PhpPlanQ1Holt.PageBase("/en")),
            on,
            off,
            bar["count"]!,
            bar["hidden"]!,
            PhpPlanQ1Holt.SyncUrl("/en/accessories-spare-parts", state),
            PhpPlanQ1Holt.Text(read.TryGetValue("q", out var q) ? q : ""),
            PhpPlanQ1Holt.Text(read.TryGetValue("make", out var make) ? make : ""),
            PhpPlanQ1Holt.Text(read.TryGetValue("page", out var page) ? page : ""),
            PhpPlanQ1Holt.FacetList(rows, "slug", "label", "", "All categories"),
            PhpPlanQ1Holt.FacetList(rows, "slug", "label", "brakes", "All categories"),
            PhpPlanQ1Holt.FillVehicleMakes(new object?[] { Map(("make", "Toyota")), Map(("make", "BMW")), "" }, "Toyota"),
            PhpPlanQ1Holt.RenderPager(1, 1),
            PhpPlanQ1Holt.RenderPager(3, 8),
            PhpPlanQ1Holt.RenderPager(3, 8, true),
            PhpPlanQ1Holt.Load(state),
            PhpPlanQ1Holt.CountLabel(Map(("from", 1), ("to", 24), ("total", 40)), new Dictionary<string, object?>(StringComparer.Ordinal)),
            PhpPlanQ1Holt.CountLabel(Map(("total", 0)), new Dictionary<string, object?>(StringComparer.Ordinal)),
            PhpPlanQ1Holt.CountLabel(Map(("total", 1)), Map(("id", "99"), ("showSavedOnly", false))),
            PhpPlanQ1Holt.SetView("grid"),
            PhpPlanQ1Holt.SetView("list"),
            PhpPlanQ1Holt.SaveNotify("yawer@ecomae.com", Map(
                ("q", "oil"), ("category", "car-care"), ("subcategory", ""), ("make", "Toyota"),
                ("model", ""), ("city", "Dubai"), ("condition", "new"), ("price_min", "10"), ("price_max", "90")))
        };
    }

    private static object Page()
    {
        var html = PhpPlanQ1Holt.RenderPage("/en");
        return new object[]
        {
            PhpPlanQ1Holt.PageFlags(html),
            html.Contains("Set-Cookie", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("function esc(s)", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("function listingHref(item)", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("function saveNotify(email)", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("epc_acc_saved_ads", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Live()
    {
        var item = SampleItem();
        var baseHref = PhpPlanQ1Holt.PageBase("/en");
        var cats = new[]
        {
            Map(("slug", "filters"), ("label", "Filters"), ("count", 3), ("subs", new List<object?> { Map(("slug", "cabin"), ("label", "Cabin"), ("count", 1)) })),
            Map(("slug", "brakes"), ("label", "Brakes"), ("count", 2), ("subs", new List<object?> { Map(("slug", "pads"), ("label", "Pads"), ("count", 2)) }))
        };
        var browseCats = string.Concat(cats.Select(c =>
            "<button type=\"button\" data-cat=\"" + PhpPlanQ1Holt.Esc(c["slug"]) + "\">" + PhpPlanQ1Holt.Esc(c["label"])
            + (PhpPlanQ1Holt.JsTruthy(c["count"]) ? " (" + PhpPlanQ1Holt.Text(c["count"]) + ")" : "") + "</button>"));
        var detail = PhpPlanQ1Holt.RenderDetail(item, baseHref);
        PhpPlanQ1Holt.SavedIds = ["42"];
        var savedDetail = PhpPlanQ1Holt.RenderDetail(item, baseHref);
        return new object[]
        {
            detail,
            savedDetail,
            PhpPlanQ1Holt.RenderCards([], Map(("id", "")), true, baseHref),
            PhpPlanQ1Holt.RenderCards([], Map(("id", ""), ("showSavedOnly", true)), true, baseHref),
            browseCats,
            PhpPlanQ1Holt.ListingHref(item, baseHref),
            PhpPlanQ1Holt.BrowseBackHref(item, baseHref),
            PhpPlanQ1Holt.Money(120, "AED"),
            PhpPlanQ1Holt.Esc(item["title"])
        };
    }

    private static object Tenant()
    {
        var ar = PhpPlanQ1Holt.RenderPage("/ar/");
        var en = PhpPlanQ1Holt.RenderPage("/en");
        var item = Map(("id", "7"), ("category", "brakes"));
        return new object[]
        {
            PhpPlanQ1Holt.PageFlags(ar),
            PhpPlanQ1Holt.PageFlags(en),
            ar.Contains("data-lang=\"/ar\"", StringComparison.Ordinal) ? 1 : 0,
            en.Contains("data-lang=\"/en\"", StringComparison.Ordinal) ? 1 : 0,
            ar.Contains("data-lang=\"/en\"", StringComparison.Ordinal) ? 1 : 0,
            en.Contains("data-lang=\"/ar\"", StringComparison.Ordinal) ? 1 : 0,
            PhpPlanQ1Holt.PageBase("/ar/"),
            PhpPlanQ1Holt.PageBase("/en"),
            PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/ar/")),
            PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/en")),
            PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/ar/")).Contains("/en/", StringComparison.Ordinal) ? 1 : 0,
            PhpPlanQ1Holt.ListingHref(item, PhpPlanQ1Holt.PageBase("/en")).Contains("/ar/", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static Dictionary<string, object?> SampleItem()
        => Map(
            ("id", "42"),
            ("title", "Cabin Filter <OEM>"),
            ("category", "filters"),
            ("subcategory", "cabin"),
            ("category_label", "Filters"),
            ("subcategory_label", "Cabin"),
            ("make", "Toyota"),
            ("model", "Corolla"),
            ("year", "2018"),
            ("city", "Dubai"),
            ("condition", "new"),
            ("price", 120),
            ("compare_price", 180),
            ("currency", "AED"),
            ("stock_qty", 3),
            ("updated_at", 1699999280),
            ("description", "Fits Corolla 2015-2019 & \"Yaris\"."),
            ("external_url", "https://seller.example/ad"),
            ("photos", new List<object?> { "https://cdn/a.jpg", Map(("url", "https://cdn/b.jpg")) }),
            ("featured", true),
            ("photo_count", 2));

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] pairs)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
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
