using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1FordParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Ford");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Ford_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/portal/epc_boc_product_brochure.php", PhpPlanQ1Ford.BocProductBrochurePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Ford.Reset();
        PhpPlanQ1Ford.IsSuperCp = () => true;
        PhpPlanQ1Ford.BackendDir = "cp";
        var html = PhpPlanQ1Ford.EpcBocProductBrochure();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Ford.BocProductBrochurePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Equal("/cp/control/cp_brochure", PhpPlanQ1Ford.LastCpBrochure);
        Assert.Contains("/brochure-cp", PhpPlanQ1Ford.Denied, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Ford.Reset();
        return name switch
        {
            "empty" => Empty(),
            "super" => Super(),
            "escape" => Escape(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var none = PhpPlanQ1Ford.EpcBocProductBrochure();
        PhpPlanQ1Ford.IsSuperCp = () => false;
        PhpPlanQ1Ford.BackendDir = "acme";
        var tenant = PhpPlanQ1Ford.EpcBocProductBrochure();
        return new object[] { none, tenant };
    }

    private static object Super()
    {
        PhpPlanQ1Ford.IsSuperCp = () => true;
        PhpPlanQ1Ford.BackendDir = "cp";
        var plain = PhpPlanQ1Ford.EpcBocProductBrochure();
        PhpPlanQ1Ford.FrameFileExists = true;
        PhpPlanQ1Ford.HasFrameOpen = true;
        PhpPlanQ1Ford.HasFrameClose = true;
        var framed = PhpPlanQ1Ford.EpcBocProductBrochure();
        PhpPlanQ1Ford.FrameFileExists = false;
        PhpPlanQ1Ford.HasFrameOpen = true;
        PhpPlanQ1Ford.HasFrameClose = true;
        var closeOnly = PhpPlanQ1Ford.EpcBocProductBrochure();
        return new object[] { plain, framed, closeOnly };
    }

    private static object Escape()
    {
        PhpPlanQ1Ford.IsSuperCp = () => true;
        PhpPlanQ1Ford.BackendDir = "";
        var emptyDir = PhpPlanQ1Ford.EpcBocProductBrochure();
        PhpPlanQ1Ford.BackendDir = "acme\"><x";
        PhpPlanQ1Ford.FrameFileExists = true;
        PhpPlanQ1Ford.HasFrameOpen = true;
        PhpPlanQ1Ford.HasFrameClose = true;
        var html = PhpPlanQ1Ford.EpcBocProductBrochure();
        return new object[] { html, emptyDir };
    }

    private static object Tenant()
    {
        PhpPlanQ1Ford.IsSuperCp = () => true;
        PhpPlanQ1Ford.BackendDir = "acme";
        PhpPlanQ1Ford.FrameFileExists = true;
        PhpPlanQ1Ford.HasFrameOpen = true;
        PhpPlanQ1Ford.HasFrameClose = true;
        var acme = PhpPlanQ1Ford.EpcBocProductBrochure();
        PhpPlanQ1Ford.BackendDir = "beta";
        var beta = PhpPlanQ1Ford.EpcBocProductBrochure();
        PhpPlanQ1Ford.IsSuperCp = () => false;
        PhpPlanQ1Ford.BackendDir = "acme";
        var denied = PhpPlanQ1Ford.EpcBocProductBrochure();
        return new object[] { acme, beta, denied };
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
