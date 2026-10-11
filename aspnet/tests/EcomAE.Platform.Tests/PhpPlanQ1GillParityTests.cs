using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1GillParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Gill");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Gill_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/portal/epc_boc_channel_control.php", PhpPlanQ1Gill.BocChannelControlPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Gill.Reset();
        PhpPlanQ1Gill.IsSuperCp = () => true;
        PhpPlanQ1Gill.IsAdmin = () => true;
        PhpPlanQ1Gill.HasPdo = true;
        PhpPlanQ1Gill.HasUserClass = true;
        PhpPlanQ1Gill.HasGetName = true;
        PhpPlanQ1Gill.HasGetUserId = true;
        PhpPlanQ1Gill.HasNavForUser = true;
        PhpPlanQ1Gill.BackendDir = "cp";
        PhpPlanQ1Gill.OperatorName = "Ada";
        PhpPlanQ1Gill.OperatorId = 7;
        var html = PhpPlanQ1Gill.EpcBocChannelControl();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Gill.BocChannelControlPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Equal("/cp", PhpPlanQ1Gill.LastBase);
        Assert.Contains("Super CP", PhpPlanQ1Gill.DeniedSuper, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Gill.Reset();
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
        var none = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.IsSuperCp = () => false;
        PhpPlanQ1Gill.IsAdmin = () => true;
        PhpPlanQ1Gill.BackendDir = "acme";
        var tenant = PhpPlanQ1Gill.EpcBocChannelControl();
        return new object[] { none, tenant };
    }

    private static object Gates()
    {
        PhpPlanQ1Gill.IsSuperCp = () => true;
        PhpPlanQ1Gill.IsAdmin = () => false;
        PhpPlanQ1Gill.BackendDir = "acme";
        var login = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.BackendDir = "acme\"><x";
        var escape = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.IsAdmin = () => true;
        PhpPlanQ1Gill.BackendDir = "cp";
        var noPdo = PhpPlanQ1Gill.EpcBocChannelControl();
        var notPdo = PhpPlanQ1Gill.EpcBocChannelControl();
        return new object[] { login, escape, noPdo, notPdo };
    }

    private static object Live()
    {
        PhpPlanQ1Gill.IsSuperCp = () => true;
        PhpPlanQ1Gill.IsAdmin = () => true;
        PhpPlanQ1Gill.HasPdo = true;
        PhpPlanQ1Gill.HasUserClass = true;
        PhpPlanQ1Gill.HasGetName = true;
        PhpPlanQ1Gill.HasGetUserId = true;
        PhpPlanQ1Gill.BackendDir = "cp";
        PhpPlanQ1Gill.OperatorName = "";
        PhpPlanQ1Gill.OperatorId = 0;
        var fleet = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.HasNavForUser = true;
        PhpPlanQ1Gill.OperatorName = "Ada";
        PhpPlanQ1Gill.OperatorId = 7;
        var named = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.BackendDir = "";
        var emptyDir = PhpPlanQ1Gill.EpcBocChannelControl();
        return new object[] { fleet, named, emptyDir };
    }

    private static object Tenant()
    {
        PhpPlanQ1Gill.IsSuperCp = () => true;
        PhpPlanQ1Gill.IsAdmin = () => true;
        PhpPlanQ1Gill.HasPdo = true;
        PhpPlanQ1Gill.HasUserClass = true;
        PhpPlanQ1Gill.HasGetName = true;
        PhpPlanQ1Gill.HasGetUserId = true;
        PhpPlanQ1Gill.HasNavForUser = true;
        PhpPlanQ1Gill.OperatorName = "Ada";
        PhpPlanQ1Gill.OperatorId = 7;
        PhpPlanQ1Gill.BackendDir = "acme";
        var acme = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.BackendDir = "beta";
        var beta = PhpPlanQ1Gill.EpcBocChannelControl();
        PhpPlanQ1Gill.IsSuperCp = () => false;
        PhpPlanQ1Gill.BackendDir = "acme";
        var denied = PhpPlanQ1Gill.EpcBocChannelControl();
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
