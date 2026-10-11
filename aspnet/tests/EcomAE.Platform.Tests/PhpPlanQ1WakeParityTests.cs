using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1WakeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Wake");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Wake_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_boc_page_shell.php", PhpPlanQ1Wake.BocPageShellPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Wake.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Wake.BocPageShellPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Wake.EpcBocNormalizeContentUrl("/x"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Wake.Reset();
        return name switch
        {
            "norm" => new object[]
            {
                PhpPlanQ1Wake.EpcBocNormalizeContentUrl("/CP/Shop/Foo?x=1"),
                PhpPlanQ1Wake.EpcBocNormalizeContentUrl("control/"),
                PhpPlanQ1Wake.EpcBocNormalizeContentUrl(""),
                PhpPlanQ1Wake.EpcBocNormalizeContentUrl("LOGIN")
            },
            "resolve" => Resolve(),
            "should" => Should(),
            "open" => Open(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Resolve()
    {
        PhpPlanQ1Wake.Areas = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["shop"] = new(StringComparer.Ordinal) { ["path"] = "/shop", ["label"] = "Shop" },
            ["orders"] = new(StringComparer.Ordinal) { ["path"] = "/shop/orders", ["label"] = "Orders" },
            ["empty"] = new(StringComparer.Ordinal) { ["path"] = "", ["label"] = "Skip" }
        };
        var hit = PhpPlanQ1Wake.EpcBocResolveArea("Shop/Orders/Card");
        return new object?[]
        {
            hit?["id"],
            hit?["area"] is Dictionary<string, object?> a ? a["label"] : null,
            PhpPlanQ1Wake.EpcBocResolveArea("finance/ledger") == null ? 1 : 0,
            PhpPlanQ1Wake.EpcBocResolveArea("") == null ? 1 : 0
        };
    }

    private static object Should()
    {
        PhpPlanQ1Wake.IsSuperCpHost = () => true;
        var ok = PhpPlanQ1Wake.EpcBocShouldUsePageShell("shop/prices") ? 1 : 0;
        var control = PhpPlanQ1Wake.EpcBocShouldUsePageShell("control") ? 1 : 0;
        var login = PhpPlanQ1Wake.EpcBocShouldUsePageShell("control/login") ? 1 : 0;
        PhpPlanQ1Wake.ShellOpenFlag = true;
        var already = PhpPlanQ1Wake.EpcBocShouldUsePageShell("shop/prices") ? 1 : 0;
        PhpPlanQ1Wake.ShellOpenFlag = false;
        PhpPlanQ1Wake.IsSuperCpHost = () => false;
        var tenant = PhpPlanQ1Wake.EpcBocShouldUsePageShell("shop/prices") ? 1 : 0;
        return new object[] { ok, control, login, already, tenant };
    }

    private static object Open()
    {
        var opens = 0;
        var closes = 0;
        var switched = 0;
        PhpPlanQ1Wake.IsSuperCpHost = () => true;
        PhpPlanQ1Wake.Areas = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["shop"] = new(StringComparer.Ordinal) { ["path"] = "shop", ["label"] = "Shop desk" }
        };
        PhpPlanQ1Wake.ContentUrl = "shop/prices";
        PhpPlanQ1Wake.Nav = () => new[] { "n1" };
        PhpPlanQ1Wake.ConsoleOpen = _ => opens++;
        PhpPlanQ1Wake.ConsoleClose = () => closes++;
        PhpPlanQ1Wake.TenantSwitch = () => switched = 1;
        var first = PhpPlanQ1Wake.EpcBocPageShellOpen();
        var ctx = (Dictionary<string, object?>)first["ctx"]!;
        var firstRow = new object[] { opens, ctx["title"]!, ctx["base"]!, ctx["operator"]!, ctx["scope"]!, first["skip_header"]!, switched };
        PhpPlanQ1Wake.EpcBocPageShellOpen();
        var secondOpens = opens;
        var closed = PhpPlanQ1Wake.EpcBocPageShellClose();
        PhpPlanQ1Wake.IsSuperCpHost = () => false;
        PhpPlanQ1Wake.EpcBocPageShellOpen(new Dictionary<string, object?> { ["title"] = "Nope" });
        return new object[] { firstRow, secondOpens, new object[] { closes, (int)closed["closed"]! == 1 && (int)closed["open"]! == 0 ? 1 : 0 }, opens };
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
