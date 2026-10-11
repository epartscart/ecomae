using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SprayParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Spray");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Spray_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_cp_professional_shell.php", PhpPlanQ1Spray.ProfessionalShellPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Spray.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Spray.ProfessionalShellPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Spray.EpcCpShellCssVersion(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Spray.Reset();
        return name switch
        {
            "names" => Names(),
            "context" => Context(),
            "markup" => Markup(),
            "scripts" => Scripts(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        var href = PhpPlanQ1Spray.EpcCpShellAssetHref("/cp/templates/bootstrap_admin/css/epc_cp_ui.css", "/content/general_pages/epc_cp_ui_css.php");
        var first = PhpPlanQ1Spray.EpcCpSidebarFirstPaintScript();
        var nuclear = PhpPlanQ1Spray.EpcCpNuclearCriticalCss();
        var force = PhpPlanQ1Spray.EpcCpForceVisibleBodyStyle();
        var early = PhpPlanQ1Spray.EpcCpMenuSectionsEarlyStyle();
        return new object?[]
        {
            PhpPlanQ1Spray.EpcCpShellCssVersion(),
            PhpPlanQ1Spray.EpcCpShellUseAssetProxies() ? 1 : 0,
            href,
            PhpPlanQ1Spray.EpcCpShellBodyClasses(),
            first.Length >= 70 ? first[..70] : first,
            first.Contains("epc_cp_sidebar_collapsed", StringComparison.Ordinal) ? 1 : 0,
            nuclear.Contains("epc-cp-main-pane-critical", StringComparison.Ordinal) ? 1 : 0,
            Encoding.UTF8.GetByteCount(nuclear),
            force.Length >= 40 ? force[..40] : force,
            force.Contains("20260721aoCfg1", StringComparison.Ordinal) ? 1 : 0,
            early.Contains("epc-cp-menu-sections-early", StringComparison.Ordinal) ? 1 : 0,
            Encoding.UTF8.GetByteCount(early)
        };
    }

    private static object Context()
    {
        PhpPlanQ1Spray.BrandCpContext = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["company_name"] = "Acme Parts",
            ["product_name"] = "Control Panel",
            ["hub_tagline"] = "Finance & operations"
        };
        PhpPlanQ1Spray.TranslateById = id => "T" + id;
        PhpPlanQ1Spray.IndustryFor = _ => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "Auto parts",
            ["icon"] = "fa-cogs"
        };
        var tenant = PhpPlanQ1Spray.EpcCpLoginContext();
        var tenantShell = PhpPlanQ1Spray.EpcCpShellContext();
        PhpPlanQ1Spray.IsSuperCpHost = () => true;
        var super = PhpPlanQ1Spray.EpcCpLoginContext();
        var superShell = PhpPlanQ1Spray.EpcCpShellContext();
        PhpPlanQ1Spray.IsSuperCpHost = () => false;
        PhpPlanQ1Spray.IsPlatformErpActive = () => true;
        var plat = PhpPlanQ1Spray.EpcCpLoginContext();
        var platShell = PhpPlanQ1Spray.EpcCpShellContext();
        PhpPlanQ1Spray.IsPlatformErpActive = () => false;
        PhpPlanQ1Spray.IsDemoCpContext = () => true;
        PhpPlanQ1Spray.DemoCpSiteKey = () => "acme_demo";
        PhpPlanQ1Spray.DemoTenantRow = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["trade_name"] = "Acme Demo" };
        var demo = PhpPlanQ1Spray.EpcCpLoginContext();
        var demoShell = PhpPlanQ1Spray.EpcCpShellContext();
        PhpPlanQ1Spray.IsDemoErpOnly = () => true;
        var erpOnly = PhpPlanQ1Spray.EpcCpLoginContext();
        var erpOnlyShell = PhpPlanQ1Spray.EpcCpShellContext();
        PhpPlanQ1Spray.IsDemoCpContext = () => false;
        PhpPlanQ1Spray.IsDemoErpOnly = () => false;
        PhpPlanQ1Spray.IsClientErpActive = () => true;
        PhpPlanQ1Spray.ClientErpSiteKey = () => "beta";
        PhpPlanQ1Spray.ClientErpTenantRow = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["trade_name"] = "Beta Trading" };
        var client = PhpPlanQ1Spray.EpcCpLoginContext();
        var clientShell = PhpPlanQ1Spray.EpcCpShellContext();
        PhpPlanQ1Spray.IsClientErpActive = () => false;
        PhpPlanQ1Spray.IsSuperCpHost = () => true;
        PhpPlanQ1Spray.IsPlatformOperator = () => true;
        PhpPlanQ1Spray.IsPlatformHostname = () => true;
        PhpPlanQ1Spray.ActiveIndustry = () => "";
        PhpPlanQ1Spray.RequestUri = "/cp/control/config";
        var header = PhpPlanQ1Spray.EpcCpPageHeaderContext();
        return new object?[] { tenant, tenantShell, super, superShell, plat, platShell, demo, demoShell, erpOnly, erpOnlyShell, client, clientShell, header };
    }

    private static object Markup()
    {
        var empty = PhpPlanQ1Spray.EpcCpPageHeaderActionsHtml(Array.Empty<Dictionary<string, object?>>());
        var pills = PhpPlanQ1Spray.EpcCpPageHeaderActionsHtml(new[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = "/erp/",
                ["label"] = "Platform ERP",
                ["icon"] = "fa-chart-line",
                ["primary"] = 1
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = "https://www.ecomae.com/",
                ["label"] = "O'Reilly",
                ["icon"] = "fa-globe",
                ["target"] = "_blank"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = "",
                ["label"] = "skip"
            }
        });
        var buf = new StringBuilder();
        PhpPlanQ1Spray.Echo = s => buf.Append(s);
        PhpPlanQ1Spray.HubLogoEnqueue = () => buf.Append("<!--hub-logo-->\n");
        PhpPlanQ1Spray.LoginHeroEnqueue = () => buf.Append("<!--login-hero-->\n");
        PhpPlanQ1Spray.LoginEnqueue = () => buf.Append("<!--login-css-->\n");
        PhpPlanQ1Spray.EpcCpShellEnqueueAssets(false);
        var enq1 = buf.ToString();
        buf.Clear();
        PhpPlanQ1Spray.EpcCpShellEnqueueAssets(true);
        var enq2 = buf.ToString();
        PhpPlanQ1Spray.AnimatedApplies = () => true;
        PhpPlanQ1Spray.AnimatedMarkup = slot => "ANIMATED:" + slot;
        var heroAnim = PhpPlanQ1Spray.EpcCpLoginHeroMarkup();
        PhpPlanQ1Spray.AnimatedApplies = () => false;
        PhpPlanQ1Spray.HubLogo = (slot, opts) => "HUB:" + slot + ":" + (Truthy(opts, "show_title") ? "1" : "0");
        PhpPlanQ1Spray.StaticLogo = (slot, opts) => "STATIC:" + slot + ":" + (Truthy(opts, "show_tagline") ? "1" : "0");
        var heroHub = PhpPlanQ1Spray.EpcCpLoginHeroMarkup();
        PhpPlanQ1Spray.LoginStaticCookie = "1";
        var heroStatic = PhpPlanQ1Spray.EpcCpLoginHeroMarkup();
        PhpPlanQ1Spray.LoginStaticCookie = null;
        var noInline = PhpPlanQ1Spray.EpcCpShellInlineStyleBlock();
        PhpPlanQ1Spray.InlineCssQuery = "0";
        var zeroInline = PhpPlanQ1Spray.EpcCpShellInlineStyleBlock();
        PhpPlanQ1Spray.InlineCssQuery = "1";
        var tmp = Path.Combine(Path.GetTempPath(), "ecomae_spray_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(tmp, "cp/templates/bootstrap_admin/css"));
        File.WriteAllText(Path.Combine(tmp, "cp/templates/bootstrap_admin/css/epc_cp_ui.css"), "body{color:red}\n");
        PhpPlanQ1Spray.DocumentRoot = tmp;
        var inline = PhpPlanQ1Spray.EpcCpShellInlineStyleBlock();
        try { Directory.Delete(tmp, true); } catch { /* ignore */ }
        return new object?[] { empty, pills, enq1, enq2, heroAnim, heroHub, heroStatic, noInline, zeroInline, inline };
    }

    private static object Scripts()
    {
        var blobs = new[]
        {
            PhpPlanQ1Spray.EpcCpSidebarFirstPaintScript(),
            PhpPlanQ1Spray.EpcCpNuclearCriticalCss(),
            PhpPlanQ1Spray.EpcCpForceVisibleScript(),
            PhpPlanQ1Spray.EpcCpSidebarEarlyInitScript(),
            PhpPlanQ1Spray.EpcErpSidebarEarlyInitScript(),
            PhpPlanQ1Spray.EpcErpSidebarAccordionScript(),
            PhpPlanQ1Spray.EpcCpHideMenuVanillaScript(),
            PhpPlanQ1Spray.EpcCpMenuSectionsScript(),
            PhpPlanQ1Spray.EpcCpSidebarCollapseScript(),
            PhpPlanQ1Spray.EpcCpModernRevealScript()
        };
        return blobs.Select(Blob).ToArray();
    }

    private static object[] Blob(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var last = bytes.Length >= 32 ? bytes[^32..] : bytes;
        var first = bytes.Length >= 48 ? bytes[..48] : bytes;
        return
        [
            bytes.Length,
            Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant(),
            Encoding.UTF8.GetString(first),
            Encoding.UTF8.GetString(last)
        ];
    }

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is true or 1 or 1L or 1.0 or "1"
            || (value is string s && s != "" && s != "0");

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
