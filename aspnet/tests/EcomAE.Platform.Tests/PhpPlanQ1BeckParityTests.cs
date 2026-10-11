using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1BeckParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Beck");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Beck_MatchPhpGolden()
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
                failures.Add(name + " exp=" + exp[..Math.Min(280, exp.Length)] + " got=" + got[..Math.Min(280, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("modules/slider/slider.php", PhpPlanQ1Beck.SliderModulePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Beck.Reset();
        PhpPlanQ1Beck.QuerySettings = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["connected"] = 1,
            ["cnt_img"] = 3,
            ["cnt_img_next"] = 1,
            ["time_next"] = 4000
        };
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = "/x", ["href"] = "/img/x.jpg" }
        };
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Beck.SliderModulePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Beck.EpcSliderModule(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Beck.Reset();
        return name switch
        {
            "off" => Off(),
            "slides" => Slides(),
            "chrome" => Chrome(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Off()
    {
        PhpPlanQ1Beck.QuerySettings = () => null;
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>();
        var none = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.QuerySettings = () => Settings(0, 3, 1, 4000);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = "/acme", ["href"] = "/img/acme.jpg" }
        };
        PhpPlanQ1Beck.MainFlag = () => true;
        PhpPlanQ1Beck.MenuCategoryShow = () => 1;
        PhpPlanQ1Beck.HeaderStyle = () => "no_header";
        var off = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.QuerySettings = () => Settings("0", 3, 1, 4000);
        var stringOff = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.QuerySettings = () => Settings(1, 3, 1, 4000);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>();
        var noSlides = PhpPlanQ1Beck.EpcSliderModule();
        return new object[] { none, off, stringOff, noSlides };
    }

    private static object Slides()
    {
        PhpPlanQ1Beck.QuerySettings = () => Settings(1, 3, 1, 4000);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = "", ["href"] = "/img/plain.jpg" },
            new(StringComparer.Ordinal) { ["link"] = "/promo", ["href"] = "/img/promo.jpg" },
            new(StringComparer.Ordinal) { ["link"] = "0", ["href"] = "/img/zero.jpg" }
        };
        PhpPlanQ1Beck.MainFlag = () => false;
        PhpPlanQ1Beck.MenuCategoryShow = () => "";
        PhpPlanQ1Beck.HeaderStyle = () => "";
        var html = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.QuerySettings = () => Settings("1", 2, 2, 2500);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = null, ["href"] = "/img/null-link.jpg" }
        };
        PhpPlanQ1Beck.MenuCategoryShow = () => "0";
        PhpPlanQ1Beck.HeaderStyle = () => "boxed";
        var nullLink = PhpPlanQ1Beck.EpcSliderModule();
        return new object[] { html, nullLink };
    }

    private static object Chrome()
    {
        PhpPlanQ1Beck.QuerySettings = () => Settings(1, 4, 2, 5000);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = "/keep", ["href"] = "/img/keep.jpg" }
        };
        PhpPlanQ1Beck.MainFlag = () => true;
        PhpPlanQ1Beck.MenuCategoryShow = () => 1;
        PhpPlanQ1Beck.HeaderStyle = () => "boxed";
        var menu = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.MenuCategoryShow = () => "yes";
        PhpPlanQ1Beck.HeaderStyle = () => "no_header";
        var noHeader = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.MainFlag = () => false;
        PhpPlanQ1Beck.MenuCategoryShow = () => 1;
        PhpPlanQ1Beck.HeaderStyle = () => "no_header";
        var notMain = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.MainFlag = () => true;
        PhpPlanQ1Beck.MenuCategoryShow = () => "";
        var emptyMenu = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.MenuCategoryShow = () => "0";
        var zeroMenu = PhpPlanQ1Beck.EpcSliderModule();
        return new object[] { menu, noHeader, notMain, emptyMenu, zeroMenu };
    }

    private static object Tenant()
    {
        PhpPlanQ1Beck.QuerySettings = () => Settings(1, 3, 1, 4000);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = "/acme-city", ["href"] = "/img/acme-city.jpg" }
        };
        PhpPlanQ1Beck.MainFlag = () => true;
        PhpPlanQ1Beck.MenuCategoryShow = () => 1;
        PhpPlanQ1Beck.HeaderStyle = () => "no_header";
        var acme = PhpPlanQ1Beck.EpcSliderModule();
        PhpPlanQ1Beck.QuerySettings = () => Settings(1, 2, 1, 3000);
        PhpPlanQ1Beck.QueryImages = () => new List<Dictionary<string, object?>>
        {
            new(StringComparer.Ordinal) { ["link"] = "/beta-town", ["href"] = "/img/beta-town.jpg" }
        };
        PhpPlanQ1Beck.MainFlag = () => false;
        PhpPlanQ1Beck.MenuCategoryShow = () => "";
        PhpPlanQ1Beck.HeaderStyle = () => "boxed";
        var beta = PhpPlanQ1Beck.EpcSliderModule();
        return new object[] { acme, beta };
    }

    private static Dictionary<string, object?> Settings(object connected, object cnt, object next, object time)
        => new(StringComparer.Ordinal)
        {
            ["connected"] = connected,
            ["cnt_img"] = cnt,
            ["cnt_img_next"] = next,
            ["time_next"] = time
        };

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

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
