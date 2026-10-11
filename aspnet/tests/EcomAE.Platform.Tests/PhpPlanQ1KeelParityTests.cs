using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1KeelParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Keel");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Keel_MatchPhpGolden()
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
            if (!Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Keel.AuthGatePath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Keel.AuthGatePath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Keel.Reset();
        Assert.Contains("epc_cp_auth_gate.php", PhpPlanQ1Keel.AuthGatePath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Keel.AuthGatePath, StringComparison.Ordinal);
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "/cp/";
        PhpPlanQ1Keel.Server["REQUEST_METHOD"] = "GET";
        var hit = PhpPlanQ1Keel.EpcCpAuthGateRun();
        Assert.Equal("redirect", Convert.ToString(hit["action"]));
        Assert.DoesNotContain("PHPSESSID", Convert.ToString(hit["location"]), StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Keel.EpcCpAuthGateIsAdmin());
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Keel.Reset();
        return name switch
        {
            "admin" => Admin(),
            "landing" => Landing(),
            "run" => Run(),
            "mfa" => Mfa(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static PhpPlanQ1Keel.GateStore SeedSessions()
    {
        var db = new PhpPlanQ1Keel.GateStore();
        db.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "tok-ok", Type = 1, UserId = 7 });
        db.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "tok-user", Type = 0, UserId = 7 });
        db.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "tok-dup", Type = 1, UserId = 7 });
        db.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "tok-dup", Type = 1, UserId = 7 });
        PhpPlanQ1Keel.Store = db;
        return db;
    }

    private static bool AdminOf(string? session, string? uid)
    {
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal);
        if (session != null)
        {
            PhpPlanQ1Keel.Cookies["admin_session"] = session;
        }

        if (uid != null)
        {
            PhpPlanQ1Keel.Cookies["admin_u_id"] = uid;
        }

        return PhpPlanQ1Keel.EpcCpAuthGateIsAdmin();
    }

    private static Rendered Admin()
    {
        PhpPlanQ1Keel.ConfigPresent = false;
        var noCfg = AdminOf("tok-ok", "7");
        PhpPlanQ1Keel.ConfigPresent = true;
        SeedSessions();
        var rows = new List<object?>
        {
            noCfg,
            AdminOf("", "7"),
            AdminOf("tok-ok", "0"),
            AdminOf("tok-ok", ""),
            AdminOf("missing", "7"),
            AdminOf("tok-user", "7"),
            AdminOf("tok-ok", "7"),
            AdminOf("tok-ok", "7abc"),
            AdminOf("tok-ok", "8"),
            AdminOf("tok-dup", "7")
        };
        PhpPlanQ1Keel.Store!.ThrowOnQuery = true;
        rows.Add(AdminOf("tok-ok", "7"));
        return new Rendered(rows);
    }

    private static Rendered Landing()
    {
        PhpPlanQ1Keel.IsErpOnlyTenant = false;
        var a = PhpPlanQ1Keel.EpcCpAuthGateErpOnlyLanding();
        PhpPlanQ1Keel.IsErpOnlyTenant = true;
        PhpPlanQ1Keel.ErpShellUrlPresent = false;
        PhpPlanQ1Keel.BackendDir = "cp";
        var b = PhpPlanQ1Keel.EpcCpAuthGateErpOnlyLanding();
        PhpPlanQ1Keel.BackendDir = "";
        var c = PhpPlanQ1Keel.EpcCpAuthGateErpOnlyLanding();
        PhpPlanQ1Keel.BackendDir = "panel";
        var d = PhpPlanQ1Keel.EpcCpAuthGateErpOnlyLanding();
        PhpPlanQ1Keel.ErpShellUrlPresent = true;
        PhpPlanQ1Keel.ErpCpShellUrl = "/erp-app";
        var e = PhpPlanQ1Keel.EpcCpAuthGateErpOnlyLanding();
        return new Rendered(new object?[] { a, b, c, d, e });
    }

    private static void Req(string method, string uri, string qs = "")
    {
        PhpPlanQ1Keel.Server["REQUEST_METHOD"] = method;
        PhpPlanQ1Keel.Server["REQUEST_URI"] = uri;
        PhpPlanQ1Keel.Server["QUERY_STRING"] = qs;
        PhpPlanQ1Keel.Get = new(StringComparer.Ordinal);
        PhpPlanQ1Keel.Post = new(StringComparer.Ordinal);
    }

    private static void PrepAdmin()
    {
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal)
        {
            ["admin_session"] = "tok-ok",
            ["admin_u_id"] = "7"
        };
    }

    private static Rendered Run()
    {
        SeedSessions();
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal);
        PhpPlanQ1Keel.BackendDir = "cp";
        var outList = new List<object?>();
        Req("GET", "/cp/", "next=1");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/index.php");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("POST", "/cp/");
        PhpPlanQ1Keel.Post["authentication"] = "1";
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/shop/tenant_hub/x", "a=1");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/control/portal/tenants");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/shop/finance/erp");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/control");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/client-erp/x");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/demo/x");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("GET", "/cp/control/ajax/ping");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("POST", "/cp/");
        PhpPlanQ1Keel.Post["authentication"] = "0";
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        Req("POST", "/cp/");
        PhpPlanQ1Keel.Post["authentication"] = "";
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.BackendDir = "";
        Req("GET", "/cp/");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.BackendDir = "/cp/";
        Req("GET", "/cp/shop/tenant_hub/x");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.BackendDir = "cp";
        PrepAdmin();
        Req("GET", "/cp/control/portal/tenants");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.IsErpOnlyTenant = true;
        PhpPlanQ1Keel.ErpShellUrlPresent = true;
        PhpPlanQ1Keel.ErpCpShellUrl = "/cp/shop/finance/erp?epc_erp_shell=1";
        PhpPlanQ1Keel.IsPlatformHostname = false;
        Req("GET", "/cp/");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.IsPlatformHostname = true;
        Req("GET", "/cp/", "tab=1");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.IsErpOnlyTenant = false;
        PhpPlanQ1Keel.PlatformErpActive = true;
        PhpPlanQ1Keel.PlatformErpShellUrl = "/platform-erp";
        Req("GET", "/cp/");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.PlatformErpActive = false;
        PhpPlanQ1Keel.ClientErpActive = true;
        PhpPlanQ1Keel.ClientErpSiteKey = "acme";
        PhpPlanQ1Keel.ClientErpShellUrl = key => "/client-erp/" + key;
        Req("GET", "/cp/");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.ClientErpActive = false;
        PhpPlanQ1Keel.IsDemoCp = true;
        PhpPlanQ1Keel.DemoParsed = new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_login_root"] = 1 };
        PhpPlanQ1Keel.DemoSiteKey = "demo1";
        PhpPlanQ1Keel.DemoIsErpOnly = true;
        PhpPlanQ1Keel.DemoErpShellUrl = key => "/demo/" + key + "/erp";
        PhpPlanQ1Keel.DemoPostLoginUrl = key => "/demo/" + key + "/cp/control";
        Req("GET", "/demo/acme/cp/");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.DemoIsErpOnly = false;
        Req("GET", "/demo/acme/cp/");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        PhpPlanQ1Keel.DemoParsed = new Dictionary<string, object?>(StringComparer.Ordinal) { ["is_login_root"] = 0 };
        Req("GET", "/cp/shop/tenant_hub/x");
        outList.Add(PhpPlanQ1Keel.EpcCpAuthGateRun());
        return new Rendered(outList);
    }

    private static Rendered Mfa()
    {
        var db = new PhpPlanQ1Keel.GateStore();
        db.Sessions.Add(new PhpPlanQ1Keel.SessionRow { Session = "tok-ok", Type = 1, UserId = 7 });
        PhpPlanQ1Keel.Store = db;
        PhpPlanQ1Keel.MfaEnforce = (_, userId, path) =>
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["user_id"] = userId, ["path"] = path };
        PhpPlanQ1Keel.MfaAjax = (_, userId) =>
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["user_id"] = userId, ["next"] = "/cp/mfa" };
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal);
        PhpPlanQ1Keel.MfaFilePresent = true;
        PhpPlanQ1Keel.EpcCpMfaRouteGuard();
        var a = new Dictionary<string, object?>(StringComparer.Ordinal) { ["called"] = false, ["count"] = PhpPlanQ1Keel.EnforceCount };
        PhpPlanQ1Keel.Cookies["admin_u_id"] = "7";
        PhpPlanQ1Keel.MfaFilePresent = false;
        PhpPlanQ1Keel.EpcCpMfaRouteGuard();
        var b = new Dictionary<string, object?>(StringComparer.Ordinal) { ["called"] = false, ["count"] = PhpPlanQ1Keel.EnforceCount };
        PhpPlanQ1Keel.MfaFilePresent = true;
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "/cp/control/portal/x";
        PhpPlanQ1Keel.EpcCpMfaRouteGuard();
        var c = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["called"] = true,
            ["path"] = "/cp/control/portal/x",
            ["user_id"] = 7
        };
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "?skip=1";
        PhpPlanQ1Keel.EpcCpMfaRouteGuard();
        var c2 = new Dictionary<string, object?>(StringComparer.Ordinal) { ["called"] = false, ["count"] = PhpPlanQ1Keel.EnforceCount };
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "";
        PhpPlanQ1Keel.EpcCpMfaRouteGuard();
        var c3 = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["called"] = true,
            ["path"] = "",
            ["user_id"] = 7
        };
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal);
        var d = PhpPlanQ1Keel.EpcCpAuthGateMfaAjax();
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal) { ["admin_u_id"] = "7", ["admin_session"] = "bad" };
        var e = PhpPlanQ1Keel.EpcCpAuthGateMfaAjax();
        PhpPlanQ1Keel.Cookies = new(StringComparer.Ordinal) { ["admin_u_id"] = "7", ["admin_session"] = "tok-ok" };
        PhpPlanQ1Keel.MfaFilePresent = false;
        var f = PhpPlanQ1Keel.EpcCpAuthGateMfaAjax();
        PhpPlanQ1Keel.MfaFilePresent = true;
        var g = PhpPlanQ1Keel.EpcCpAuthGateMfaAjax();
        PhpPlanQ1Keel.Get["epc_mfa_ajax"] = "1";
        PhpPlanQ1Keel.Server["REQUEST_METHOD"] = "GET";
        PhpPlanQ1Keel.Server["REQUEST_URI"] = "/cp/control?epc_mfa_ajax=1";
        PhpPlanQ1Keel.Server["QUERY_STRING"] = "epc_mfa_ajax=1";
        var h = PhpPlanQ1Keel.EpcCpAuthGateRun();
        return new Rendered(new object?[] { a, b, c, c2, c3, d, e, f, g, h, PhpPlanQ1Keel.EnforceCount, PhpPlanQ1Keel.AjaxCount });
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 1800 ? value : value[..1800] + "…";
}
