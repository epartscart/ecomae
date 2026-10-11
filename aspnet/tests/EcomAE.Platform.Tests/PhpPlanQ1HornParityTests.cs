using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HornParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Horn");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Horn_MatchPhpGolden()
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
                failures.Add(name + " exp=" + exp[..Math.Min(360, exp.Length)] + " got=" + got[..Math.Min(360, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/content/control/portal/epc_boc_audit_log.php", PhpPlanQ1Horn.BocAuditLogPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Horn.Reset();
        PhpPlanQ1Horn.IsSuperCp = () => true;
        PhpPlanQ1Horn.IsAdmin = () => true;
        PhpPlanQ1Horn.HasPdo = true;
        SeedAudit();
        var html = PhpPlanQ1Horn.EpcBocAuditLogPage();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Horn.BocAuditLogPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-boc-audit-log\"", html, StringComparison.Ordinal);
        Assert.Contains("Ada", html, StringComparison.Ordinal);
        Assert.Contains("tenant_control", html, StringComparison.Ordinal);
        PhpPlanQ1Horn.AreaQuerySet = true;
        PhpPlanQ1Horn.AreaQuery = "governance";
        var filtered = PhpPlanQ1Horn.EpcBocAuditLogPage();
        Assert.Contains("Filtered to area", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant_control", filtered, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Horn.Reset();
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
        var none = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.IsSuperCp = () => false;
        PhpPlanQ1Horn.IsAdmin = () => true;
        PhpPlanQ1Horn.BackendDir = "acme";
        PhpPlanQ1Horn.AreaQuerySet = true;
        PhpPlanQ1Horn.AreaQuery = "governance";
        var tenant = PhpPlanQ1Horn.EpcBocAuditLogPage();
        return new object[] { none, tenant };
    }

    private static object Gates()
    {
        PhpPlanQ1Horn.IsSuperCp = () => true;
        PhpPlanQ1Horn.IsAdmin = () => false;
        PhpPlanQ1Horn.BackendDir = "acme";
        var login = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.BackendDir = "acme\"><x";
        var escape = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.IsAdmin = () => true;
        PhpPlanQ1Horn.BackendDir = "cp";
        var noPdo = PhpPlanQ1Horn.EpcBocAuditLogPage();
        var notPdo = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.IsAdmin = () => false;
        PhpPlanQ1Horn.BackendDir = "";
        var emptyDir = PhpPlanQ1Horn.EpcBocAuditLogPage();
        return new object[] { login, escape, noPdo, notPdo, emptyDir };
    }

    private static object Live()
    {
        PhpPlanQ1Horn.IsSuperCp = () => true;
        PhpPlanQ1Horn.IsAdmin = () => true;
        PhpPlanQ1Horn.HasPdo = true;
        PhpPlanQ1Horn.BackendDir = "cp";
        var blank = PhpPlanQ1Horn.EpcBocAuditLogPage();
        SeedAudit();
        var all = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.AreaQuerySet = true;
        PhpPlanQ1Horn.AreaQuery = "governance";
        var gov = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.AreaQuery = "Governance!!";
        var dirty = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.AreaQuery = "tenant_control";
        var tenantArea = PhpPlanQ1Horn.EpcBocAuditLogPage();
        return new object[] { blank, all, gov, dirty, tenantArea };
    }

    private static object Tenant()
    {
        PhpPlanQ1Horn.IsSuperCp = () => true;
        PhpPlanQ1Horn.IsAdmin = () => true;
        PhpPlanQ1Horn.HasPdo = true;
        SeedAudit();
        PhpPlanQ1Horn.AreaQuerySet = true;
        PhpPlanQ1Horn.AreaQuery = "governance";
        PhpPlanQ1Horn.BackendDir = "acme";
        var acme = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.BackendDir = "beta";
        var beta = PhpPlanQ1Horn.EpcBocAuditLogPage();
        PhpPlanQ1Horn.IsSuperCp = () => false;
        PhpPlanQ1Horn.BackendDir = "acme";
        var denied = PhpPlanQ1Horn.EpcBocAuditLogPage();
        return new object[] { acme, beta, denied };
    }

    private static void SeedAudit()
    {
        PhpPlanQ1Horn.AuditRows = new List<Dictionary<string, object?>>
        {
            Row(1, 1700000000, 7, "Ada", "governance", "toggle", "acme", "{\"via\":\"test\",\"n\":1}", "1.2.3.4"),
            Row(2, 1700000100, 0, "", "tenant_control", "reveal", "beta", null, "10.0.0.1"),
            Row(3, 1700000200, 9, "Ada & Co", "governance", "<script>", "\"", "not-json", "")
        };
    }

    private static Dictionary<string, object?> Row(
        long id, long ts, int userId, string actor, string area, string action, string target, string? meta, string ip)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["ts"] = ts,
            ["user_id"] = userId,
            ["actor"] = actor,
            ["area"] = area,
            ["action"] = action,
            ["target"] = target,
            ["meta"] = meta,
            ["ip"] = ip
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
