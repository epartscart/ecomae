using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HoweParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Howe");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Howe_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/portal/epc_mobile_apps.php", PhpPlanQ1Howe.MobileAppsPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Howe.Reset();
        PhpPlanQ1Howe.IsAdmin = () => true;
        PhpPlanQ1Howe.IsSuperCp = () => false;
        PhpPlanQ1Howe.BackendDir = "cp";
        PhpPlanQ1Howe.HttpHost = "WWW.Example.COM";
        var tenant = PhpPlanQ1Howe.EpcMobileAppsPage();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Howe.MobileAppsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", tenant, StringComparison.Ordinal);
        Assert.Contains("id=\"epc-mobile-form\"", tenant, StringComparison.Ordinal);
        Assert.Contains("FRAME_OPEN:epc-mobile-apps|Tenant CP|Mobile apps (Android / iOS / PWA)", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("firebase_project_id", tenant, StringComparison.Ordinal);
        Assert.Contains("https://www.example.com/en/", tenant, StringComparison.Ordinal);
        Assert.Contains("epartscart://", tenant, StringComparison.Ordinal);
        PhpPlanQ1Howe.IsSuperCp = () => true;
        PhpPlanQ1Howe.MobileOverride = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["enabled"] = true,
            ["firebase_project_id"] = "acme-fire",
            ["push_enabled"] = true
        };
        var super = PhpPlanQ1Howe.EpcMobileAppsPage();
        Assert.Contains("FRAME_OPEN:epc-mobile-apps|Super CP|Mobile apps (Android / iOS / PWA)", super, StringComparison.Ordinal);
        Assert.Contains("firebase_project_id", super, StringComparison.Ordinal);
        Assert.Contains("acme-fire", super, StringComparison.Ordinal);
        Assert.Contains("name=\"push_enabled\"", super, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", super, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Howe.Reset();
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
        var none = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsAdmin = () => false;
        PhpPlanQ1Howe.IsSuperCp = () => true;
        PhpPlanQ1Howe.BackendDir = "acme";
        PhpPlanQ1Howe.HttpHost = "acme.example.com";
        var login = PhpPlanQ1Howe.EpcMobileAppsPage();
        return new object[] { none, login };
    }

    private static object Gates()
    {
        PhpPlanQ1Howe.IsAdmin = () => true;
        PhpPlanQ1Howe.IsSuperCp = null;
        PhpPlanQ1Howe.BackendDir = "cp";
        PhpPlanQ1Howe.HttpHost = "cp.example.com";
        var noSuper = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsSuperCp = () => false;
        PhpPlanQ1Howe.BackendDir = "";
        var emptyDir = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.BackendDir = "acme\"><x";
        var escape = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.BackendDir = "cp";
        var tenantGate = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsAdmin = () => false;
        PhpPlanQ1Howe.IsSuperCp = () => true;
        var denied = PhpPlanQ1Howe.EpcMobileAppsPage();
        return new object[] { noSuper, emptyDir, escape, tenantGate, denied };
    }

    private static object Live()
    {
        PhpPlanQ1Howe.IsAdmin = () => true;
        PhpPlanQ1Howe.IsSuperCp = () => false;
        PhpPlanQ1Howe.BackendDir = "cp";
        PhpPlanQ1Howe.HttpHost = "WWW.Example.COM";
        var tenantDefault = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsSuperCp = () => true;
        PhpPlanQ1Howe.HttpHost = "example.com";
        var superDefault = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.MobileOverride = FilledMobile();
        var filled = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsSuperCp = () => false;
        PhpPlanQ1Howe.MobileOverride = new Dictionary<string, object?>(StringComparer.Ordinal);
        PhpPlanQ1Howe.HttpHost = "www.epartscart.com";
        var wwwHost = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.HttpHost = "epartscart.com";
        PhpPlanQ1Howe.MobileOverride = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["api_base_url"] = "0"
        };
        var apex = PhpPlanQ1Howe.EpcMobileAppsPage();
        return new object[] { tenantDefault, superDefault, filled, wwwHost, apex };
    }

    private static object Tenant()
    {
        PhpPlanQ1Howe.IsAdmin = () => true;
        PhpPlanQ1Howe.IsSuperCp = () => true;
        PhpPlanQ1Howe.BackendDir = "acme";
        PhpPlanQ1Howe.HttpHost = "acme.example.com";
        var acme = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsSuperCp = () => false;
        PhpPlanQ1Howe.BackendDir = "beta";
        PhpPlanQ1Howe.HttpHost = "beta.example.com";
        var beta = PhpPlanQ1Howe.EpcMobileAppsPage();
        PhpPlanQ1Howe.IsSuperCp = () => true;
        PhpPlanQ1Howe.BackendDir = "acme";
        PhpPlanQ1Howe.HttpHost = "ignored.example.com";
        PhpPlanQ1Howe.HasPortalHost = true;
        PhpPlanQ1Howe.PortalHost = "shop.acme.test";
        var portalHost = PhpPlanQ1Howe.EpcMobileAppsPage();
        return new object[] { acme, beta, portalHost };
    }

    private static Dictionary<string, object?> FilledMobile()
        => new(StringComparer.Ordinal)
        {
            ["enabled"] = true,
            ["app_name"] = "Ada & Co",
            ["bundle_id"] = "com.acme.app",
            ["deep_link_scheme"] = "acme://",
            ["deep_link_domain"] = "www.acme.com",
            ["api_base_url"] = "https://shop.acme.com/en/",
            ["play_store_url"] = "https://play.google.com/x",
            ["app_store_url"] = "https://apps.apple.com/x",
            ["pwa_enabled"] = false,
            ["firebase_project_id"] = "acme-fire",
            ["push_enabled"] = true
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
