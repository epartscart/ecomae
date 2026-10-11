using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ThroatParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Throat");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Throat_MatchPhpGolden()
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
        => Assert.Equal("cp/content/shop/order_process/epc_order_staff_summary.php", PhpPlanQ1Throat.OrderStaffSummaryPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Throat.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 1, 1, 1, 1, 1, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Throat.OrderStaffSummaryPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Throat.Reset();
        return name switch
        {
            "empty" => new object[] { Clip(PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?>(), 0, 1, 1, 1, 1, 1)), Clip(PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 0, 1, 1, 1, 1, 1)) },
            "nocrm" => NoCrm(),
            "crm" => Crm(),
            "fmt" => Fmt(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object NoCrm()
    {
        PhpPlanQ1Throat.CrmUserId = _ => 0;
        PhpPlanQ1Throat.ProfileHtml = (_, _) => "<table class=\"epc-kv\"><tr><td>Name</td><td>Acme</td></tr></table>";
        var html = PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 9 }, 9, 3, 100.0, 80.0, 20.0, 20.0);
        return new object[] { Clip(html), html.Contains("Relationship manager", StringComparison.Ordinal) ? 1 : 0, html.Contains("100.00", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Crm()
    {
        PhpPlanQ1Throat.CrmUserId = _ => 44;
        PhpPlanQ1Throat.ProfileHtml = (_, _) => "<p>O'Neil</p>";
        var html = PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 2 }, 2, 8, 10.5, 4.2, 6.3, 60.0);
        return new object[] { Clip(html), html.Contains("Relationship manager user ID: <strong>44</strong>", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Fmt()
    {
        PhpPlanQ1Throat.CrmUserId = _ => 0;
        PhpPlanQ1Throat.ProfileHtml = (_, _) => "";
        var html = PhpPlanQ1Throat.EpcOrderStaffSummaryRenderHtml(new Dictionary<string, object?> { ["id"] = 1 }, 1, 1, 1234.5, 1000, 234.5, 19);
        return new object[]
        {
            Clip(html),
            html.Contains("1,234.50", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("61.73", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("19.00", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object[] Clip(string html)
        => [PhpStrlen(html), PhpSubstr(html, 0, 80), PhpSubstr(html, -80, 80)];

    private static int PhpStrlen(string value) => Encoding.UTF8.GetByteCount(value);

    private static string PhpSubstr(string value, int start, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (start < 0)
        {
            start = bytes.Length + start;
        }

        if (start < 0)
        {
            start = 0;
        }

        if (start >= bytes.Length)
        {
            return "";
        }

        return Encoding.UTF8.GetString(bytes, start, Math.Min(length, bytes.Length - start));
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
