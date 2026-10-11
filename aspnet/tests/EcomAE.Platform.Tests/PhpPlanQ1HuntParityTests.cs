using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HuntParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hunt");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hunt_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/portal/epc_tenant_email_settings.php", PhpPlanQ1Hunt.TenantEmailSettingsPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hunt.Reset();
        PhpPlanQ1Hunt.IsAdmin = () => true;
        PhpPlanQ1Hunt.IsSuperCp = () => false;
        PhpPlanQ1Hunt.FeatureEnabled = () => true;
        PhpPlanQ1Hunt.BackendDir = "cp";
        PhpPlanQ1Hunt.Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = "config.php",
            ["host"] = "smtp.example.com",
            ["port"] = "465"
        };
        var html = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hunt.TenantEmailSettingsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-tenant-smtp-form\"", html, StringComparison.Ordinal);
        Assert.Contains("smtp.example.com:465", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Modern auth settings", html, StringComparison.Ordinal);
        PhpPlanQ1Hunt.IsSuperCp = () => true;
        PhpPlanQ1Hunt.FeatureEnabled = () => false;
        var disabled = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        Assert.Contains("Modern auth settings", disabled, StringComparison.Ordinal);
        Assert.Contains("disabled for this tenant", disabled, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-tenant-smtp-form", disabled, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hunt.Reset();
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
        var none = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.IsAdmin = () => false;
        PhpPlanQ1Hunt.IsSuperCp = () => true;
        PhpPlanQ1Hunt.FeatureEnabled = () => true;
        PhpPlanQ1Hunt.BackendDir = "acme";
        var login = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        return new object[] { none, login };
    }

    private static object Gates()
    {
        PhpPlanQ1Hunt.IsAdmin = () => true;
        PhpPlanQ1Hunt.IsSuperCp = null;
        PhpPlanQ1Hunt.FeatureEnabled = () => false;
        PhpPlanQ1Hunt.BackendDir = "cp";
        var noSuper = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.IsSuperCp = () => false;
        PhpPlanQ1Hunt.FeatureEnabled = () => true;
        PhpPlanQ1Hunt.BackendDir = "";
        PhpPlanQ1Hunt.AdminEmail = "ops@example.com";
        var emptyDir = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.BackendDir = "acme\"><x";
        var escape = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.BackendDir = "cp";
        var tenantGate = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.IsAdmin = () => false;
        PhpPlanQ1Hunt.IsSuperCp = () => true;
        PhpPlanQ1Hunt.AdminEmail = "";
        var denied = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        return new object[] { noSuper, emptyDir, escape, tenantGate, denied };
    }

    private static object Live()
    {
        PhpPlanQ1Hunt.IsAdmin = () => true;
        PhpPlanQ1Hunt.IsSuperCp = () => false;
        PhpPlanQ1Hunt.FeatureEnabled = () => true;
        PhpPlanQ1Hunt.BackendDir = "cp";
        var defaults = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = "config.php",
            ["host"] = "smtp.example.com",
            ["port"] = "465",
            ["encryption"] = "ssl",
            ["from_name"] = "Ada & Co",
            ["from_email"] = "ada@example.com"
        };
        PhpPlanQ1Hunt.AdminEmail = "ops@example.com";
        var diag = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal) { ["source"] = "tenant" };
        PhpPlanQ1Hunt.Integrations = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["smtp"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["use_tenant_smtp"] = true,
                ["smtp_host"] = "mail.acme.com",
                ["smtp_port"] = "587",
                ["smtp_encryption"] = "none",
                ["smtp_username"] = "acme & ops",
                ["smtp_password"] = "secret",
                ["from_name"] = "Acme",
                ["from_email"] = "noreply@acme.com"
            }
        };
        PhpPlanQ1Hunt.AdminEmail = "Ada <ada@acme.com>";
        var filled = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal);
        PhpPlanQ1Hunt.Integrations = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["smtp"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["smtp_encryption"] = ""
            }
        };
        PhpPlanQ1Hunt.AdminEmail = "";
        var blankEnc = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.IsSuperCp = () => true;
        PhpPlanQ1Hunt.FeatureEnabled = () => false;
        PhpPlanQ1Hunt.Integrations = new Dictionary<string, object?>(StringComparer.Ordinal);
        var featureOff = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        return new object[] { defaults, diag, filled, blankEnc, featureOff };
    }

    private static object Tenant()
    {
        PhpPlanQ1Hunt.IsAdmin = () => true;
        PhpPlanQ1Hunt.IsSuperCp = () => true;
        PhpPlanQ1Hunt.FeatureEnabled = () => true;
        PhpPlanQ1Hunt.BackendDir = "acme";
        PhpPlanQ1Hunt.Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal) { ["source"] = "acme" };
        PhpPlanQ1Hunt.AdminEmail = "ada@acme.test";
        var acme = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.IsSuperCp = () => false;
        PhpPlanQ1Hunt.BackendDir = "beta";
        PhpPlanQ1Hunt.Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal) { ["source"] = "beta" };
        PhpPlanQ1Hunt.AdminEmail = "bea@beta.test";
        var beta = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
        PhpPlanQ1Hunt.IsAdmin = () => false;
        PhpPlanQ1Hunt.IsSuperCp = () => true;
        PhpPlanQ1Hunt.BackendDir = "acme";
        PhpPlanQ1Hunt.AdminEmail = "";
        var denied = PhpPlanQ1Hunt.EpcTenantEmailSettingsPage();
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
