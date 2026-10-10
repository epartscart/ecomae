using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ReachParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Reach");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Reach_MatchPhpGolden()
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
                failures.Add(name + " exp=" + exp[..Math.Min(350, exp.Length)] + " got=" + got[..Math.Min(350, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("modules/shop/geo/point_geo_node.php", PhpPlanQ1Reach.PointGeoNodePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Reach.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Reach.PointGeoNodePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Reach.Reset();
        return name switch
        {
            "flat" => Flat(),
            "tree" => Tree(),
            "page" => Page(),
            "single" => Single(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Flat()
    {
        PhpPlanQ1Reach.TranslateById = Tr;
        PhpPlanQ1Reach.QueryRows = () => [];
        var empty = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderFlatList);
        PhpPlanQ1Reach.QueryFailed = () => true;
        var missing = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderFlatList);
        PhpPlanQ1Reach.QueryFailed = () => false;
        PhpPlanQ1Reach.QueryRows = () =>
        [
            Row(1, "100", 1, 10),
            Row(2, "Dubai", 2, 20),
            Row(12, "O'Reilly <x>", 3, 30)
        ];
        var rows = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderFlatList);
        return new object[] { empty, missing, rows };
    }

    private static object Tree()
    {
        var bad = PhpPlanQ1Reach.Capture(() => PhpPlanQ1Reach.PrintGeoNodes("nope"));
        var deep = PhpPlanQ1Reach.Capture(() => PhpPlanQ1Reach.PrintGeoNodes(
            new List<object?> { new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 1, ["value"] = "X", ["level"] = 1 } }, 33));
        var tree = PhpPlanQ1Reach.Capture(() => PhpPlanQ1Reach.PrintGeoNodes(new List<object?>
        {
            "skip",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 1, ["value"] = "UAE", ["level"] = 1,
                ["data"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 2, ["value"] = "Dubai", ["level"] = 2 },
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 12, ["value"] = "O'Reilly <x>", ["level"] = 3 }
                }
            }
        }));
        return new object[] { bad, deep, tree };
    }

    private static object Page()
    {
        PhpPlanQ1Reach.TranslateById = Tr;
        PhpPlanQ1Reach.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
        SeedAcme();
        var noCookie = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule);
        PhpPlanQ1Reach.MyCity = "12";
        var acme = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule);
        PhpPlanQ1Reach.MyCity = "99";
        var invalid = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule);
        SeedBeta();
        PhpPlanQ1Reach.MyCity = "1";
        var beta = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule);
        return new object[] { noCookie, acme, invalid, beta };
    }

    private static object Single()
    {
        PhpPlanQ1Reach.TranslateById = Tr;
        PhpPlanQ1Reach.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
        var rows = new List<Dictionary<string, object?>> { Row(7, "Solo", 3, 1) };
        PhpPlanQ1Reach.QueryRows = () => rows;
        PhpPlanQ1Reach.CountRows = () => 1;
        PhpPlanQ1Reach.QuerySingle = () => rows[0];
        PhpPlanQ1Reach.QueryById = id => id == "7" ? rows[0] : null;
        var auto = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule);
        PhpPlanQ1Reach.MyCity = "7";
        var kept = PhpPlanQ1Reach.Capture(PhpPlanQ1Reach.EpcGeoRenderModule);
        return new object[] { auto, kept };
    }

    private static void SeedAcme()
    {
        var rows = new List<Dictionary<string, object?>> { Row(1, "100", 1, 10), Row(12, "12", 3, 20) };
        PhpPlanQ1Reach.QueryRows = () => rows;
        PhpPlanQ1Reach.CountRows = () => 2;
        PhpPlanQ1Reach.QueryFirst = () => rows[0];
        PhpPlanQ1Reach.QueryById = id => rows.FirstOrDefault(r => Str(r["id"]) == id);
    }

    private static void SeedBeta()
    {
        var rows = new List<Dictionary<string, object?>> { Row(1, "Beta Town", 3, 10), Row(2, "Beta Port", 3, 20) };
        PhpPlanQ1Reach.QueryRows = () => rows;
        PhpPlanQ1Reach.CountRows = () => 2;
        PhpPlanQ1Reach.QueryFirst = () => rows[0];
        PhpPlanQ1Reach.QueryById = id => rows.FirstOrDefault(r => Str(r["id"]) == id);
        PhpPlanQ1Reach.TranslateById = raw => raw == "4771" ? "Choose your city" : raw;
    }

    private static string Tr(string raw) => raw switch
    {
        "4771" => "Choose your city",
        "100" => "O'Reilly",
        "12" => "Acme City",
        _ => raw
    };

    private static Dictionary<string, object?> Row(int id, string value, int level, int order)
        => new(StringComparer.Ordinal) { ["id"] = id, ["value"] = value, ["level"] = level, ["order"] = order };

    private static string Str(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";

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
