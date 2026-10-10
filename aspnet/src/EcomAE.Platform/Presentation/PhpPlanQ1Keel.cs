using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-keel Super CP auth gate. PHP identifiers kept for the inventory:
/// <c>epc_cp_auth_gate_pdo</c>, <c>epc_cp_auth_gate_is_admin</c>,
/// <c>epc_cp_auth_gate_erp_only_landing</c>, <c>epc_cp_auth_gate_run</c>,
/// <c>epc_cp_mfa_route_guard</c>, <c>epc_cp_auth_gate_mfa_ajax</c>.
/// Portal / demo / platform-ERP / MFA parents stay injected.
/// GET never mints a session cookie.
/// </summary>
public static class PhpPlanQ1Keel
{
    public const string AuthGatePath = "cp/epc_cp_auth_gate.php";

    public sealed class SessionRow
    {
        public string Session { get; set; } = "";
        public int Type { get; set; }
        public int UserId { get; set; }
    }

    public sealed class GateStore
    {
        public bool Missing { get; set; }
        public bool ThrowOnQuery { get; set; }
        public List<SessionRow> Sessions { get; } = [];
    }

    public static Dictionary<string, string> Cookies { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Get { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Post { get; set; } = new(StringComparer.Ordinal);
    public static string BackendDir { get; set; } = "cp";
    public static bool ConfigPresent { get; set; } = true;
    public static GateStore? Store { get; set; }
    public static Func<GateStore?>? OpenPdo { get; set; }
    public static bool IsErpOnlyTenant { get; set; }
    public static string? ErpCpShellUrl { get; set; }
    public static bool ErpShellUrlPresent { get; set; }
    public static bool IsDemoCp { get; set; }
    public static Dictionary<string, object?>? DemoParsed { get; set; }
    public static Func<string, string>? ControlUrl { get; set; }
    public static bool DemoIsErpOnly { get; set; }
    public static Func<string, string>? DemoErpShellUrl { get; set; }
    public static string DemoSiteKey { get; set; } = "";
    public static Func<string, string>? DemoPostLoginUrl { get; set; }
    public static bool PlatformErpActive { get; set; }
    public static string PlatformErpShellUrl { get; set; } = "";
    public static bool ClientErpActive { get; set; }
    public static string ClientErpSiteKey { get; set; } = "";
    public static Func<string, string>? ClientErpShellUrl { get; set; }
    public static bool IsPlatformHostname { get; set; }
    public static bool PlatformHostnameFnPresent { get; set; } = true;
    public static bool MfaFilePresent { get; set; }
    public static Func<GateStore, int, string, Dictionary<string, object?>>? MfaEnforce { get; set; }
    public static Func<GateStore, int, Dictionary<string, object?>>? MfaAjax { get; set; }
    public static int EnforceCount { get; set; }
    public static int AjaxCount { get; set; }

    private static GateStore? _cached;

    public static void Reset()
    {
        Cookies = new(StringComparer.Ordinal);
        Server = new(StringComparer.Ordinal) { ["REQUEST_URI"] = "/", ["REQUEST_METHOD"] = "GET", ["QUERY_STRING"] = "" };
        Get = new(StringComparer.Ordinal);
        Post = new(StringComparer.Ordinal);
        BackendDir = "cp";
        ConfigPresent = true;
        Store = null;
        OpenPdo = null;
        IsErpOnlyTenant = false;
        ErpCpShellUrl = null;
        ErpShellUrlPresent = false;
        IsDemoCp = false;
        DemoParsed = null;
        ControlUrl = null;
        DemoIsErpOnly = false;
        DemoErpShellUrl = null;
        DemoSiteKey = "";
        DemoPostLoginUrl = null;
        PlatformErpActive = false;
        PlatformErpShellUrl = "";
        ClientErpActive = false;
        ClientErpSiteKey = "";
        ClientErpShellUrl = null;
        IsPlatformHostname = false;
        PlatformHostnameFnPresent = true;
        MfaFilePresent = false;
        MfaEnforce = null;
        MfaAjax = null;
        EnforceCount = 0;
        AjaxCount = 0;
        _cached = null;
    }

    /// <summary>PHP <c>epc_cp_auth_gate_pdo</c>.</summary>
    public static GateStore? EpcCpAuthGatePdo()
    {
        if (_cached != null)
        {
            return _cached;
        }

        if (OpenPdo != null)
        {
            _cached = OpenPdo();
            return _cached;
        }

        if (!ConfigPresent)
        {
            return null;
        }

        if (Store == null || Store.Missing)
        {
            return null;
        }

        _cached = Store;
        return _cached;
    }

    /// <summary>PHP <c>epc_cp_auth_gate_is_admin</c>.</summary>
    public static bool EpcCpAuthGateIsAdmin()
    {
        var session = Cookies.TryGetValue("admin_session", out var s) ? s : "";
        var userId = PhpInt(Cookies.TryGetValue("admin_u_id", out var u) ? u : "");
        if (session == "" || userId <= 0)
        {
            return false;
        }

        var pdo = EpcCpAuthGatePdo();
        if (pdo == null)
        {
            return false;
        }

        try
        {
            if (pdo.ThrowOnQuery)
            {
                throw new InvalidOperationException("sessions");
            }

            return pdo.Sessions.Count(row => row.Session == session && row.Type == 1 && row.UserId == userId) == 1;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>PHP <c>epc_cp_auth_gate_erp_only_landing</c>.</summary>
    public static string EpcCpAuthGateErpOnlyLanding()
    {
        if (!IsErpOnlyTenant)
        {
            return "";
        }

        if (ErpShellUrlPresent)
        {
            return ErpCpShellUrl ?? "";
        }

        // PHP uses (string) backend_dir when the property is set, including "".
        // Empty dir becomes "//shop/finance/erp?epc_erp_shell=1". run() trims; landing() does not.
        return "/" + BackendDir + "/shop/finance/erp?epc_erp_shell=1";
    }

    /// <summary>PHP <c>epc_cp_auth_gate_run</c>.</summary>
    public static Dictionary<string, object?> EpcCpAuthGateRun()
    {
        var backend = BackendDir.Trim('/');
        if (backend == "")
        {
            backend = "cp";
        }

        var cpBase = "/" + backend;
        var requestUri = Server.TryGetValue("REQUEST_URI", out var ru) ? ru : "/";
        var path = ParseUrlPath(requestUri);
        if (path is null || path == "")
        {
            path = "/";
        }
        var qs = "";
        if (Server.TryGetValue("QUERY_STRING", out var q) && q != "")
        {
            qs = "?" + q;
        }

        if (Get.ContainsKey("epc_mfa_ajax"))
        {
            return EpcCpAuthGateMfaAjax();
        }

        var requestMethod = Server.TryGetValue("REQUEST_METHOD", out var rm) && rm != "" ? rm : "GET";
        if (requestMethod == "POST" && !PhpEmpty(Post.TryGetValue("authentication", out var auth) ? auth : null))
        {
            return Continue();
        }

        var isAdmin = EpcCpAuthGateIsAdmin();
        var isDemoCp = IsDemoCp;
        var demoLoginRoot = false;
        if (isDemoCp && DemoParsed != null)
        {
            demoLoginRoot = !PhpEmpty(DemoParsed.TryGetValue("is_login_root", out var lr) ? lr : null);
        }

        var isCpRoot = path == cpBase || path == cpBase + "/" || path == cpBase + "/index.php";
        var cpControlUrl = ControlUrl?.Invoke(backend) ?? (cpBase + "/control");

        if ((isCpRoot || demoLoginRoot) && requestMethod == "GET")
        {
            if (isAdmin)
            {
                if (isDemoCp)
                {
                    if (DemoIsErpOnly && DemoErpShellUrl != null)
                    {
                        var key = DemoSiteKey;
                        if (key != "")
                        {
                            return Redirect(DemoErpShellUrl(key));
                        }
                    }

                    if (demoLoginRoot && DemoPostLoginUrl != null)
                    {
                        var key = DemoSiteKey;
                        if (key != "")
                        {
                            return Redirect(DemoPostLoginUrl(key));
                        }
                    }
                }

                if (PlatformErpActive && PlatformErpShellUrl != "")
                {
                    return Redirect(PlatformErpShellUrl);
                }

                if (ClientErpActive)
                {
                    var key = ClientErpSiteKey;
                    if (key != "" && ClientErpShellUrl != null)
                    {
                        return Redirect(ClientErpShellUrl(key));
                    }
                }

                if (!isDemoCp)
                {
                    var erpLanding = EpcCpAuthGateErpOnlyLanding();
                    if (erpLanding != "" && (!PlatformHostnameFnPresent || !IsPlatformHostname))
                    {
                        return Redirect(erpLanding);
                    }
                }
            }

            return Redirect(cpControlUrl + qs);
        }

        if (isDemoCp)
        {
            return Continue();
        }

        if (isAdmin)
        {
            return Continue();
        }

        var operatorPrefixes = new[]
        {
            cpBase + "/shop/tenant_hub/",
            cpBase + "/control/portal/",
            cpBase + "/control/ajax/",
            cpBase + "/shop/finance/erp",
            cpBase + "/client-erp/",
            cpBase + "/demo/"
        };
        foreach (var prefix in operatorPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return Redirect(cpControlUrl + qs);
            }
        }

        return Continue();
    }

    /// <summary>PHP <c>epc_cp_mfa_route_guard</c>.</summary>
    public static Dictionary<string, object?> EpcCpMfaRouteGuard()
    {
        var userId = PhpInt(Cookies.TryGetValue("admin_u_id", out var u) ? u : "");
        if (userId <= 0)
        {
            return new(StringComparer.Ordinal) { ["called"] = false };
        }

        var pdo = EpcCpAuthGatePdo();
        if (pdo == null)
        {
            return new(StringComparer.Ordinal) { ["called"] = false };
        }

        if (!MfaFilePresent)
        {
            return new(StringComparer.Ordinal) { ["called"] = false };
        }

        var requestUri = Server.TryGetValue("REQUEST_URI", out var ru) ? ru : "/";
        var path = ParseUrlPath(requestUri);
        if (path is null)
        {
            return new(StringComparer.Ordinal) { ["called"] = false };
        }

        EnforceCount++;
        var hit = MfaEnforce?.Invoke(pdo, userId, path) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        return new(StringComparer.Ordinal) { ["called"] = true, ["path"] = path, ["user_id"] = userId, ["result"] = hit };
    }

    /// <summary>PHP <c>epc_cp_auth_gate_mfa_ajax</c>.</summary>
    public static Dictionary<string, object?> EpcCpAuthGateMfaAjax()
    {
        var userId = PhpInt(Cookies.TryGetValue("admin_u_id", out var u) ? u : "");
        if (userId <= 0 || !EpcCpAuthGateIsAdmin())
        {
            return Json(new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Not authenticated" });
        }

        var pdo = EpcCpAuthGatePdo();
        if (pdo == null)
        {
            return Json(new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "Database unavailable" });
        }

        if (!MfaFilePresent)
        {
            return Json(new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "MFA module not available" });
        }

        AjaxCount++;
        var result = MfaAjax?.Invoke(pdo, userId) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        return Json(result);
    }

    private static Dictionary<string, object?> Redirect(string location)
        => new(StringComparer.Ordinal) { ["action"] = "redirect", ["location"] = location, ["status"] = 302 };

    private static Dictionary<string, object?> Continue()
        => new(StringComparer.Ordinal) { ["action"] = "continue" };

    private static Dictionary<string, object?> Json(Dictionary<string, object?> payload)
        => new(StringComparer.Ordinal)
        {
            ["action"] = "json",
            ["content_type"] = "application/json; charset=utf-8",
            ["body"] = PhpJsonEncode(payload)
        };

    /// <summary>PHP <c>parse_url($uri, PHP_URL_PATH)</c>: <c>?qs</c> is null; empty URI is <c>""</c>.</summary>
    private static string? ParseUrlPath(string requestUri)
    {
        if (requestUri.StartsWith('?'))
        {
            return null;
        }

        var q = requestUri.IndexOf('?', StringComparison.Ordinal);
        return q >= 0 ? requestUri[..q] : requestUri;
    }

    /// <summary>PHP <c>json_encode($value)</c> / <c>JSON_UNESCAPED_UNICODE</c>: slash-escape, leave unicode.</summary>
    private static string PhpJsonEncode(Dictionary<string, object?> payload)
        => JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("/", "\\/", StringComparison.Ordinal);

    private static int PhpInt(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        var i = 0;
        while (i < value.Length && char.IsWhiteSpace(value[i]))
        {
            i++;
        }

        var sign = 1;
        if (i < value.Length && (value[i] == '+' || value[i] == '-'))
        {
            sign = value[i] == '-' ? -1 : 1;
            i++;
        }

        long n = 0;
        var any = false;
        while (i < value.Length && char.IsAsciiDigit(value[i]))
        {
            any = true;
            n = (n * 10) + (value[i] - '0');
            i++;
        }

        return any ? (int)(sign * n) : 0;
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };
}
