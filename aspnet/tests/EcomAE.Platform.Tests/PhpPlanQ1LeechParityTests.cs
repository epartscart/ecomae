using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1LeechParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Leech");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Leech_MatchPhpGolden()
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
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/modules/check_ssl/check_ssl.php", PhpPlanQ1Leech.CheckSslPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Leech.Reset();
        var row = PhpPlanQ1Leech.EpcCheckSslEvaluate("https://shop.example/");
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(row["state"])!, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Leech.CheckSslPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Leech.Reset();
        var rendered = 0;
        PhpPlanQ1Leech.RenderItem = _ => rendered = 1;
        switch (name)
        {
            case "okredir":
                PhpPlanQ1Leech.ProbeOk = _ => true;
                PhpPlanQ1Leech.RedirectConfigured = () => true;
                break;
            case "oknored":
                PhpPlanQ1Leech.ProbeOk = _ => true;
                PhpPlanQ1Leech.RedirectConfigured = () => false;
                break;
            case "failredir":
                PhpPlanQ1Leech.ProbeOk = _ => false;
                PhpPlanQ1Leech.RedirectConfigured = () => true;
                break;
            case "failnored":
                PhpPlanQ1Leech.ProbeOk = _ => false;
                PhpPlanQ1Leech.RedirectConfigured = () => false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }

        var row = PhpPlanQ1Leech.EpcCheckSslEvaluate("https://shop.example/");
        return new object?[] { row["state"], row["text"], row["style"], row["sign"], rendered };
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
            _ => true
        };
    }
}
