using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1EddyParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Eddy");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Eddy_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/epc_cp_brochure_page.php", PhpPlanQ1Eddy.CpBrochurePagePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Eddy.Reset();
        PhpPlanQ1Eddy.HasConfig = true;
        PhpPlanQ1Eddy.BackendDir = "cp";
        var html = PhpPlanQ1Eddy.EpcCpBrochurePage();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Eddy.CpBrochurePagePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Equal("epartscart", PhpPlanQ1Eddy.LastOpts["brand"]);
        Assert.Equal("client", PhpPlanQ1Eddy.LastOpts["scope"]);
        Assert.Equal("/cp/control/cp_brochure", PhpPlanQ1Eddy.LastOpts["base_path"]);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Eddy.Reset();
        return name switch
        {
            "empty" => Empty(),
            "super" => Super(),
            "query" => Query(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var none = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.HasConfig = true;
        PhpPlanQ1Eddy.BackendDir = "0";
        var zeroDir = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.BackendDir = "";
        var blankDir = PhpPlanQ1Eddy.EpcCpBrochurePage();
        return new object[] { none, zeroDir, blankDir };
    }

    private static object Super()
    {
        PhpPlanQ1Eddy.HasConfig = true;
        PhpPlanQ1Eddy.BackendDir = "cp";
        PhpPlanQ1Eddy.IsSuperCp = () => true;
        var plain = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["scope"] = "client" };
        var client = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["scope"] = "" };
        var emptyScope = PhpPlanQ1Eddy.EpcCpBrochurePage();
        return new object[] { plain, client, emptyScope };
    }

    private static object Query()
    {
        PhpPlanQ1Eddy.HasConfig = true;
        PhpPlanQ1Eddy.BackendDir = "/cp/";
        PhpPlanQ1Eddy.IsSuperCp = () => false;
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scope"] = "ALL-Client!",
            ["print"] = "1"
        };
        var dirty = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.IsSuperCp = () => true;
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scope"] = "123",
            ["print"] = "0"
        };
        var digits = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.IsSuperCp = () => false;
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["print"] = "1.0" };
        var printOff = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal) { ["scope"] = "acme<script>" };
        var hostile = PhpPlanQ1Eddy.EpcCpBrochurePage();
        return new object[] { dirty, digits, printOff, hostile };
    }

    private static object Tenant()
    {
        PhpPlanQ1Eddy.HasConfig = true;
        PhpPlanQ1Eddy.BackendDir = "acme";
        PhpPlanQ1Eddy.IsSuperCp = () => false;
        var acme = PhpPlanQ1Eddy.EpcCpBrochurePage();
        PhpPlanQ1Eddy.BackendDir = "beta";
        PhpPlanQ1Eddy.IsSuperCp = () => true;
        PhpPlanQ1Eddy.Query = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scope"] = "client",
            ["print"] = "1"
        };
        var beta = PhpPlanQ1Eddy.EpcCpBrochurePage();
        return new object[] { acme, beta };
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
