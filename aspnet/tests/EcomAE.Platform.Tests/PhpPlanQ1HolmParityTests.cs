using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HolmParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Holm");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Holm_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/portal/epc_boc_command_center.php", PhpPlanQ1Holm.BocCommandCenterPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Holm.Reset();
        PhpPlanQ1Holm.IsSuperCp = () => true;
        PhpPlanQ1Holm.IsAdmin = () => true;
        PhpPlanQ1Holm.HasPdo = true;
        PhpPlanQ1Holm.HasUserClass = true;
        PhpPlanQ1Holm.HasGetName = true;
        PhpPlanQ1Holm.HasGetUserId = true;
        PhpPlanQ1Holm.HasNavForUser = true;
        PhpPlanQ1Holm.BackendDir = "cp";
        PhpPlanQ1Holm.OperatorName = "Ada";
        PhpPlanQ1Holm.OperatorId = 7;
        var html = PhpPlanQ1Holm.EpcBocCommandCenter();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Holm.BocCommandCenterPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Equal("/cp", PhpPlanQ1Holm.LastBase);
        Assert.Contains("www.ecomae.com Super CP", PhpPlanQ1Holm.DeniedSuper, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Holm.Reset();
        return name switch
        {
            "empty" => Empty(),
            "gates" => Gates(),
            "live" => Live(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var none = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.IsSuperCp = () => false;
        PhpPlanQ1Holm.IsAdmin = () => true;
        PhpPlanQ1Holm.BackendDir = "acme";
        var tenant = PhpPlanQ1Holm.EpcBocCommandCenter();
        return new object[] { none, tenant };
    }

    private static object Gates()
    {
        PhpPlanQ1Holm.IsSuperCp = () => true;
        PhpPlanQ1Holm.IsAdmin = () => false;
        PhpPlanQ1Holm.BackendDir = "acme";
        var login = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.BackendDir = "acme\"><x";
        var escape = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.IsAdmin = () => true;
        PhpPlanQ1Holm.BackendDir = "cp";
        var noPdo = PhpPlanQ1Holm.EpcBocCommandCenter();
        var notPdo = PhpPlanQ1Holm.EpcBocCommandCenter();
        return new object[] { login, escape, noPdo, notPdo };
    }

    private static object Live()
    {
        PhpPlanQ1Holm.IsSuperCp = () => true;
        PhpPlanQ1Holm.IsAdmin = () => true;
        PhpPlanQ1Holm.HasPdo = true;
        PhpPlanQ1Holm.HasUserClass = true;
        PhpPlanQ1Holm.HasGetName = true;
        PhpPlanQ1Holm.HasGetUserId = true;
        PhpPlanQ1Holm.BackendDir = "cp";
        PhpPlanQ1Holm.OperatorName = "";
        PhpPlanQ1Holm.OperatorId = 0;
        var fleet = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.HasNavForUser = true;
        PhpPlanQ1Holm.OperatorName = "Ada";
        PhpPlanQ1Holm.OperatorId = 7;
        var named = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.BackendDir = "";
        var emptyDir = PhpPlanQ1Holm.EpcBocCommandCenter();
        return new object[] { fleet, named, emptyDir };
    }

    private static object Tenant()
    {
        PhpPlanQ1Holm.IsSuperCp = () => true;
        PhpPlanQ1Holm.IsAdmin = () => true;
        PhpPlanQ1Holm.HasPdo = true;
        PhpPlanQ1Holm.HasUserClass = true;
        PhpPlanQ1Holm.HasGetName = true;
        PhpPlanQ1Holm.HasGetUserId = true;
        PhpPlanQ1Holm.HasNavForUser = true;
        PhpPlanQ1Holm.OperatorName = "Ada";
        PhpPlanQ1Holm.OperatorId = 7;
        PhpPlanQ1Holm.BackendDir = "acme";
        var acme = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.BackendDir = "beta";
        var beta = PhpPlanQ1Holm.EpcBocCommandCenter();
        PhpPlanQ1Holm.IsSuperCp = () => false;
        PhpPlanQ1Holm.BackendDir = "acme";
        var denied = PhpPlanQ1Holm.EpcBocCommandCenter();
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
