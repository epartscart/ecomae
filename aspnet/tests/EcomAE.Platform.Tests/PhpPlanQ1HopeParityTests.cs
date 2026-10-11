using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HopeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hope");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hope_MatchPhpGolden()
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
    {
        Assert.Equal("cp/content/control/portal/epc_boc_vendor_control.php", PhpPlanQ1Hope.BocVendorControlPath);
        Assert.Equal("cp/content/control/portal/epc_boc_warehouse_control.php", PhpPlanQ1Hope.BocWarehouseControlPath);
    }

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hope.Reset();
        PhpPlanQ1Hope.IsSuperCp = () => true;
        PhpPlanQ1Hope.IsAdmin = () => true;
        PhpPlanQ1Hope.HasPdo = true;
        PhpPlanQ1Hope.HasUserClass = true;
        PhpPlanQ1Hope.HasGetName = true;
        PhpPlanQ1Hope.HasGetUserId = true;
        PhpPlanQ1Hope.HasNavForUser = true;
        PhpPlanQ1Hope.BackendDir = "cp";
        PhpPlanQ1Hope.OperatorName = "Ada";
        PhpPlanQ1Hope.OperatorId = 7;
        var vendor = PhpPlanQ1Hope.EpcBocVendorControl();
        var warehouse = PhpPlanQ1Hope.EpcBocWarehouseControl();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hope.BocVendorControlPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", vendor, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", warehouse, StringComparison.Ordinal);
        Assert.Equal("/cp", PhpPlanQ1Hope.LastBase);
        Assert.Contains("VENDOR:/cp", vendor, StringComparison.Ordinal);
        Assert.Contains("WAREHOUSE:/cp", warehouse, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hope.Reset();
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
        var vNone = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.IsSuperCp = () => false;
        PhpPlanQ1Hope.IsAdmin = () => true;
        PhpPlanQ1Hope.BackendDir = "acme";
        var vTenant = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.Reset();
        var wNone = PhpPlanQ1Hope.EpcBocWarehouseControl();
        PhpPlanQ1Hope.IsSuperCp = () => false;
        PhpPlanQ1Hope.IsAdmin = () => true;
        PhpPlanQ1Hope.BackendDir = "acme";
        var wTenant = PhpPlanQ1Hope.EpcBocWarehouseControl();
        return new object[] { vNone, vTenant, wNone, wTenant };
    }

    private static object Gates()
    {
        PhpPlanQ1Hope.IsSuperCp = () => true;
        PhpPlanQ1Hope.IsAdmin = () => false;
        PhpPlanQ1Hope.BackendDir = "acme";
        var vLogin = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.BackendDir = "acme\"><x";
        var vEscape = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.IsAdmin = () => true;
        PhpPlanQ1Hope.BackendDir = "cp";
        var vNoPdo = PhpPlanQ1Hope.EpcBocVendorControl();
        var vNotPdo = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.IsAdmin = () => false;
        PhpPlanQ1Hope.BackendDir = "acme";
        var wLogin = PhpPlanQ1Hope.EpcBocWarehouseControl();
        return new object[] { vLogin, vEscape, vNoPdo, vNotPdo, wLogin };
    }

    private static object Live()
    {
        PhpPlanQ1Hope.IsSuperCp = () => true;
        PhpPlanQ1Hope.IsAdmin = () => true;
        PhpPlanQ1Hope.HasPdo = true;
        PhpPlanQ1Hope.HasUserClass = true;
        PhpPlanQ1Hope.HasGetName = true;
        PhpPlanQ1Hope.HasGetUserId = true;
        PhpPlanQ1Hope.BackendDir = "cp";
        PhpPlanQ1Hope.OperatorName = "";
        PhpPlanQ1Hope.OperatorId = 0;
        var vFleet = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.HasNavForUser = true;
        PhpPlanQ1Hope.OperatorName = "Ada";
        PhpPlanQ1Hope.OperatorId = 7;
        var vNamed = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.BackendDir = "";
        var vEmpty = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.BackendDir = "cp";
        var wNamed = PhpPlanQ1Hope.EpcBocWarehouseControl();
        return new object[] { vFleet, vNamed, vEmpty, wNamed };
    }

    private static object Tenant()
    {
        PhpPlanQ1Hope.IsSuperCp = () => true;
        PhpPlanQ1Hope.IsAdmin = () => true;
        PhpPlanQ1Hope.HasPdo = true;
        PhpPlanQ1Hope.HasUserClass = true;
        PhpPlanQ1Hope.HasGetName = true;
        PhpPlanQ1Hope.HasGetUserId = true;
        PhpPlanQ1Hope.HasNavForUser = true;
        PhpPlanQ1Hope.OperatorName = "Ada";
        PhpPlanQ1Hope.OperatorId = 7;
        PhpPlanQ1Hope.BackendDir = "acme";
        var vAcme = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.BackendDir = "beta";
        var vBeta = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.IsSuperCp = () => false;
        PhpPlanQ1Hope.BackendDir = "acme";
        var vDenied = PhpPlanQ1Hope.EpcBocVendorControl();
        PhpPlanQ1Hope.IsSuperCp = () => true;
        var wAcme = PhpPlanQ1Hope.EpcBocWarehouseControl();
        PhpPlanQ1Hope.IsSuperCp = () => false;
        var wDenied = PhpPlanQ1Hope.EpcBocWarehouseControl();
        return new object[] { vAcme, vBeta, vDenied, wAcme, wDenied };
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
