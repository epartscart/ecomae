using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1LochParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Loch");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Loch_MatchPhpGolden()
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
        => Assert.Equal("modules/shop/search_string/search_string.php", PhpPlanQ1Loch.SearchStringPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Loch.Reset();
        SeedEn();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Loch.SearchStringPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Loch.EpcSearchStringModule(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Loch.Reset();
        return name switch
        {
            "empty" => Empty(),
            "article" => Article(),
            "labels" => Labels(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        SeedEn();
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal);
        var none = PhpPlanQ1Loch.EpcSearchStringModule();
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "" };
        var blank = PhpPlanQ1Loch.EpcSearchStringModule();
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "0" };
        var zero = PhpPlanQ1Loch.EpcSearchStringModule();
        return new object[] { none, blank, zero };
    }

    private static object Article()
    {
        SeedEn();
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "C110J" };
        var plain = PhpPlanQ1Loch.EpcSearchStringModule();
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "O'Reilly <x>" };
        var special = PhpPlanQ1Loch.EpcSearchStringModule();
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "  pad  " };
        var spaces = PhpPlanQ1Loch.EpcSearchStringModule();
        return new object[] { plain, special, spaces };
    }

    private static object Labels()
    {
        PhpPlanQ1Loch.TranslateById = raw => raw switch
        {
            "4772" => "حسب القطعة",
            "4773" => "حسب الاسم",
            "4774" => "بحث",
            "2379" => "إيجاد",
            _ => raw
        };
        PhpPlanQ1Loch.LangHref = () => "/ar";
        PhpPlanQ1Loch.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-ar" };
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "فلتر" };
        var ar = PhpPlanQ1Loch.EpcSearchStringModule();
        PhpPlanQ1Loch.TranslateById = raw => raw;
        PhpPlanQ1Loch.LangHref = () => "";
        PhpPlanQ1Loch.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "" };
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "X" };
        var missing = PhpPlanQ1Loch.EpcSearchStringModule();
        return new object[] { ar, missing };
    }

    private static object Tenant()
    {
        SeedEn();
        PhpPlanQ1Loch.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-acme" };
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "AcmeCity" };
        var acme = PhpPlanQ1Loch.EpcSearchStringModule();
        PhpPlanQ1Loch.LangHref = () => "/ar";
        PhpPlanQ1Loch.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-beta" };
        PhpPlanQ1Loch.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["article"] = "BetaTown" };
        var beta = PhpPlanQ1Loch.EpcSearchStringModule();
        return new object[] { acme, beta };
    }

    private static void SeedEn()
    {
        PhpPlanQ1Loch.TranslateById = raw => raw switch
        {
            "4772" => "By article",
            "4773" => "By name",
            "4774" => "Search",
            "2379" => "Find",
            _ => raw
        };
        PhpPlanQ1Loch.LangHref = () => "/en";
        PhpPlanQ1Loch.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
    }

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
