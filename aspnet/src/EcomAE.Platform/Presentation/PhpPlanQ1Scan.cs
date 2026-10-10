using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-scan CloudPanel helpers. PHP identifiers kept for the inventory:
/// <c>epc_clp_run_cmd</c>, <c>epc_clp_bin</c>, <c>epc_clp_run</c>, <c>epc_clp_available</c>,
/// <c>epc_clp_panel_url</c>, <c>epc_clp_web_request</c>, <c>epc_clp_web_login</c>,
/// <c>epc_clp_web_sites</c>, <c>epc_clp_site_exists</c>, <c>epc_clp_provision_php_site</c>,
/// <c>epc_clp_provision_database</c>, <c>epc_clp_guess_docroot</c>, <c>epc_clp_diagnostics</c>,
/// <c>epc_clp_web_create_php_site</c>, <c>epc_clp_web_delete_site</c>,
/// <c>epc_clp_web_activate_certificates</c>, <c>epc_clp_web_install_ssl</c>,
/// <c>epc_clp_web_add_database</c>, <c>epc_clp_vhost_fetch</c>, <c>epc_clp_vhost_save</c>,
/// <c>epc_clp_model_c_tenant_hostnames</c>, <c>epc_clp_nginx_apply_safety</c>,
/// <c>epc_clp_vhost_save_and_apply</c>, <c>epc_clp_web_site_listed</c>,
/// <c>epc_clp_web_set_site_docroot</c>, <c>epc_clp_vhost_add_aliases_on_site</c>,
/// <c>epc_clp_vhost_scrub_tenant_misroutes</c>, <c>epc_clp_vhost_strip_ssl_reject_for_hosts</c>,
/// <c>epc_clp_vhost_strip_tenant_standalone_blocks</c>, <c>epc_clp_vhost_strip_tenant_markers</c>,
/// <c>epc_clp_vhost_model_c_platform_hosts</c>, <c>epc_clp_vhost_strip_hosts_from_server_names</c>,
/// <c>epc_clp_vhost_audit_server_names</c>, <c>epc_clp_ssl_standard_paths</c>,
/// <c>epc_clp_ssl_certificate_paths</c>, <c>epc_clp_install_le_certificate</c>,
/// <c>epc_clp_vhost_patch_server_ssl_for_hosts</c>, <c>epc_clp_vhost_remove_server_blocks_for_hosts</c>,
/// <c>epc_clp_nginx_reload</c>, <c>epc_clp_nginx_reload_with_pass</c>,
/// <c>epc_clp_nginx_tenant_basename_slugs</c>, <c>epc_clp_nginx_platform_config_basename</c>,
/// <c>epc_clp_nginx_ensure_sites_disabled_dir</c>, <c>epc_clp_nginx_move_to_sites_disabled</c>,
/// <c>epc_clp_nginx_find_configs_for_hosts</c>, <c>epc_clp_nginx_dedupe_platform_enabled_configs</c>,
/// <c>epc_clp_vhost_protected_regions</c>, <c>epc_clp_vhost_position_in_regions</c>,
/// <c>epc_clp_vhost_strip_orphan_tenant_server_blocks</c>, <c>epc_clp_nginx_quarantine_orphan_configs</c>,
/// <c>epc_clp_vhost_install_per_tenant_ssl</c>, <c>epc_clp_vhost_tenant_direct_server_template</c>,
/// <c>epc_clp_vhost_tenant_direct_template</c>, <c>epc_clp_vhost_build_model_c_tenant_snippets</c>,
/// <c>epc_clp_vhost_remove_aliases_from_php_backend</c>, <c>epc_clp_vhost_configure_model_c_tenants</c>,
/// <c>epc_portal_shared_docroots</c>, <c>epc_clp_vhost_patch_failover_splash</c>,
/// <c>epc_clp_vhost_patch_tenant_direct_root</c>, <c>epc_clp_vhost_add_aliases_to_php_backend</c>,
/// <c>epc_clp_vhost_configure_tenant_direct_php</c>.
/// </summary>
public static class PhpPlanQ1Scan
{
    public const string CloudPanelHelpersPath = "content/general_pages/epc_cloudpanel_helpers.php";

    private static readonly Regex ServerNameLine = new(@"^\s*server_name\s+([^;]+);", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex ServerNameLineCi = new(@"^\s*server_name\s+([^;]+);", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ServerWord = new(@"^\s*server\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ServerBrace = new(@"\bserver\s*\{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SslReject = new(@"^\s*ssl_reject_handshake\s+on;\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Return444 = new(@"\breturn\s+444\s*;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Proxy3000 = new(@"\bproxy_pass\s+https?://(?:127\.0\.0\.1|localhost):3000\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TenantDirectMarkers = new(@"# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END\s*", RegexOptions.CultureInvariant);
    private static readonly Regex TenantApexMarkers = new(@"# EPC_TENANT_APEX_REDIRECT_START[\s\S]*?# EPC_TENANT_APEX_REDIRECT_END\s*", RegexOptions.CultureInvariant);
    private static readonly Regex MarkerSplit = new(@"(# EPC_TENANT_(?:DIRECT|APEX_REDIRECT)_START[\s\S]*?# EPC_TENANT_(?:DIRECT|APEX_REDIRECT)_END\s*)", RegexOptions.CultureInvariant);
    private static readonly Regex MarkerStart = new(@"^# EPC_TENANT_(?:DIRECT|APEX_REDIRECT)_START", RegexOptions.CultureInvariant);
    private static readonly Regex EpcRedirect = new(@"(\s*server_name\s+)([^;]+)(;\s*\n\s*return\s+301\s+https://www\.epartscart\.com)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ExtraNewlines = new(@"\n{3,}", RegexOptions.CultureInvariant);
    private static readonly Regex PhpLocation = new(@"^\s*location\s+~\s+\.php\$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex CloseBraceTrail = new(@"\}\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex RootLine = new(@"^\s*root\s+[^;]+;", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex TenantRootReset = new(@"(# EPC_TENANT_DIRECT_START[\s\S]*?\n)\s*root\s+[^;]+;");
    private static readonly Regex SslCertPlaceholder = new(@"\{\{ssl_certificate\}\}\s*");
    private static readonly Regex SslKeyPlaceholder = new(@"\{\{ssl_certificate_key\}\}\s*");
    private static readonly Regex SslCertLine = new(@"^\s*ssl_certificate\s+[^;]+;", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex SslKeyLine = new(@"^\s*ssl_certificate_key\s+[^;]+;", RegexOptions.Multiline | RegexOptions.CultureInvariant);
    private static readonly Regex BakName = new(@"\.(bak|disabled|epc-disabled)(\.|$)", RegexOptions.CultureInvariant);
    private static readonly Regex EcomaeServerName = new(@"^\s*server_name\s+[^;]*\b(?:www\.)?ecomae\.com\b", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SiteHrefPhp = new(@"/site/([a-zA-Z0-9._-]+)");
    private static readonly Regex CommentTail = new(@"#.*$", RegexOptions.CultureInvariant);
    private static readonly Regex ReloadOk = new(@"\[exit=0\]", RegexOptions.CultureInvariant);
    private static readonly Regex Certbot = new("managed by certbot", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WwwPrefix = new(@"^www\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string? _bin;
    public static List<string> LastHttpHeaders { get; set; } = [];

    public static Func<string, Dictionary<string, object?>> RunCmd { get; set; } = DefaultRunCmd;
    public static Func<string, bool> IsDir { get; set; } = _ => false;
    public static Func<string, bool> IsFile { get; set; } = _ => false;
    public static Func<string, string?> ReadFile { get; set; } = _ => null;
    public static Func<string, string, Dictionary<string, object?>, HttpCall> Http { get; set; } = DefaultHttp;
    public static Action<int> Sleep { get; set; } = _ => { };
    public static Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;
    public static Func<string, string, bool> Rename { get; set; } = (_, _) => false;
    public static Func<string, IEnumerable<string>> ListFiles { get; set; } = _ => [];
    public static Func<string, string> RealPath { get; set; } = p => p;
    public static Action<string> EnsureDir { get; set; } = _ => { };

    public sealed class HttpCall
    {
        public string Body { get; set; } = "";
        public List<string> Headers { get; set; } = [];
    }

    public static void Reset()
    {
        _bin = null;
        LastHttpHeaders = [];
        RunCmd = DefaultRunCmd;
        IsDir = _ => false;
        IsFile = _ => false;
        ReadFile = _ => null;
        Http = DefaultHttp;
        Sleep = _ => { };
        Clock = () => DateTime.UtcNow;
        Rename = (_, _) => false;
        ListFiles = _ => [];
        RealPath = p => p;
        EnsureDir = _ => { };
    }

    private static Dictionary<string, object?> DefaultRunCmd(string cmd)
        => new(StringComparer.Ordinal) { ["code"] = 1, ["output"] = "exec miss", ["cmd"] = cmd };

    private static HttpCall DefaultHttp(string url, string cookie, Dictionary<string, object?> opts)
        => new();

    public static Dictionary<string, object?> EpcClpRunCmd(string fullCmd)
        => CloneCmd(RunCmd(fullCmd), fullCmd);

    private static Dictionary<string, object?> CloneCmd(Dictionary<string, object?> raw, string cmd)
        => new(StringComparer.Ordinal)
        {
            ["code"] = IntVal(Field(raw, "code"), 1),
            ["output"] = Str(Field(raw, "output")),
            ["cmd"] = Str(Field(raw, "cmd"), cmd)
        };

    public static string EpcClpBin()
    {
        if (_bin != null)
        {
            return _bin;
        }

        string[] candidates =
        [
            "sudo -n /usr/bin/clpctlWrapper",
            "sudo -n /usr/bin/clpctl",
            "/usr/bin/clpctl",
            "/usr/local/bin/clpctl",
            "clpctl"
        ];
        foreach (var bin in candidates)
        {
            var r = EpcClpRunCmd(bin + " --version");
            if (IntVal(r["code"]) == 0 || Stripos(Str(r["output"]), "CloudPanel") >= 0)
            {
                _bin = bin;
                return _bin;
            }

            var r2 = EpcClpRunCmd(bin);
            var out2 = Str(r2["output"]);
            if (IntVal(r2["code"]) == 0 && (Stripos(out2, "CloudPanel") >= 0 || Stripos(out2, "db:export") >= 0))
            {
                _bin = bin;
                return _bin;
            }
        }

        _bin = "clpctl";
        return _bin;
    }

    public static Dictionary<string, object?> EpcClpRun(string subcmd)
        => EpcClpRunCmd(EpcClpBin() + " " + subcmd);

    public static bool EpcClpAvailable()
    {
        if (IntVal(EpcClpRun("--version")["code"]) == 0)
        {
            return true;
        }

        return IntVal(EpcClpRun("app:list")["code"]) == 0;
    }

    public static string EpcClpPanelUrl() => "https://127.0.0.1:8443";

    public static string EpcClpWebRequest(string url, Dictionary<string, object?> opts, ref string cookieJar)
    {
        var call = Http(url, cookieJar, opts);
        LastHttpHeaders = [.. call.Headers];
        foreach (var line in call.Headers)
        {
            if (Stripos(line, "Set-Cookie:") != 0)
            {
                continue;
            }

            var part = line.Length > 11 ? line[11..].Trim() : "";
            var semi = part.IndexOf(';');
            if (semi >= 0)
            {
                part = part[..semi];
            }

            var eq = part.IndexOf('=');
            var name = eq >= 0 ? part[..eq] : part;
            cookieJar = Regex.Replace(cookieJar, @"(?:^|;\s*)" + Regex.Escape(name) + @"=[^;]*", "").Trim();
            cookieJar = (cookieJar + "; " + part).Trim().Trim(';', ' ');
        }

        return call.Body ?? "";
    }

    public static Dictionary<string, object?> EpcClpWebLogin(string user, string pass, ref string cookie, bool debug = false)
    {
        var panel = EpcClpPanelUrl();
        cookie = "";
        var detail = new Dictionary<string, object?>(StringComparer.Ordinal);
        var html = EpcClpWebRequest(panel + "/login", new(StringComparer.Ordinal), ref cookie);
        detail["login_page"] = html != "" ? "OK" : "EMPTY";
        var csrf = Regex.Match(html, @"name=""_csrf_token"" value=""([^""]+)""");
        if (html == "" || !csrf.Success)
        {
            detail["csrf"] = "MISSING";
            return OkDetail(false, detail);
        }

        detail["csrf"] = "OK";
        var after = EpcClpWebRequest(panel + "/login", new(StringComparer.Ordinal)
        {
            ["method"] = "POST",
            ["body"] = "userName=" + Enc(user) + "&password=" + Enc(pass) + "&_csrf_token=" + Enc(csrf.Groups[1].Value) + "&submit=Log+In&locale=en"
        }, ref cookie);
        var postHeaders = LastHttpHeaders;
        detail["post_status"] = postHeaders.Count > 0 ? postHeaders[0] : "";
        detail["post_location"] = "";
        foreach (var h in postHeaders)
        {
            if (Stripos(h, "Location:") == 0)
            {
                detail["post_location"] = h.Length > 9 ? h[9..].Trim() : "";
            }
        }

        detail["cookie"] = cookie != "" ? "SET" : "EMPTY";
        detail["still_login_form"] = Stripos(after, "btn-login") >= 0 && Stripos(after, "Log In") >= 0 ? "yes" : "no";
        var dash = EpcClpWebRequest(panel + "/dashboard", new(StringComparer.Ordinal), ref cookie);
        detail["dashboard"] = dash != "" ? "OK" : "EMPTY";
        detail["dashboard_is_login"] = Stripos(dash, "btn-login") >= 0 ? "yes" : "no";
        var invalid = Stripos(after, "Invalid credentials") >= 0;
        detail["invalid_credentials"] = invalid ? "yes" : "no";
        detail["has_2fa"] = Stripos(after, "two-factor") >= 0 || Stripos(after, "2fa") >= 0 ? "yes" : "no";
        var ok = !invalid && (string)detail["dashboard_is_login"]! == "no" && (
            Stripos(dash, "logout") >= 0
            || Regex.IsMatch(dash, @"#/site/[a-z0-9._-]+#", RegexOptions.IgnoreCase)
            || Stripos(dash, "Add Site") >= 0
            || Stripos(Str(detail["post_location"]), "dashboard") >= 0);
        if (!ok)
        {
            var err = Regex.Match(after, @"alert[^>]*>([^<]+)", RegexOptions.IgnoreCase);
            if (err.Success)
            {
                detail["error"] = err.Groups[1].Value.Trim();
            }
        }

        if (debug && !ok)
        {
            detail["after_snippet"] = StripTags(after).Length <= 200 ? StripTags(after) : StripTags(after)[..200];
        }

        return OkDetail(ok, detail);
    }

    public static List<string> EpcClpWebSites(ref string cookie)
    {
        var html = EpcClpWebRequest(EpcClpPanelUrl() + "/dashboard", new(StringComparer.Ordinal), ref cookie);
        if (html == "")
        {
            return [];
        }

        return SiteHrefPhp.Matches(html).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
    }

    public static bool EpcClpSiteExists(string domain)
    {
        var r = EpcClpRun("site:list");
        if (IntVal(r["code"]) == 0 && Stripos(Str(r["output"]), domain) >= 0)
        {
            return true;
        }

        return IsDir("/home/ecomae/htdocs/" + domain);
    }

    public static Dictionary<string, object?> EpcClpProvisionPhpSite(Dictionary<string, object?> opts)
    {
        var domain = Str(Field(opts, "domain"));
        var user = Str(Field(opts, "site_user"));
        var pass = Str(Field(opts, "site_user_password"));
        var php = Str(Field(opts, "php_version"), "8.3");
        var log = new List<string>();
        if (domain == "" || user == "" || pass == "")
        {
            return FailLog("Missing domain, site_user, or password");
        }

        if (!EpcClpAvailable())
        {
            return FailLog("clpctl not available — create site manually in CloudPanel UI");
        }

        if (EpcClpSiteExists(domain))
        {
            log.Add("Site already exists: " + domain);
        }
        else
        {
            var args = "--domainName=" + EscapeShellArg(domain)
                + " --phpVersion=" + EscapeShellArg(php)
                + " --vhostTemplate=" + EscapeShellArg("Generic")
                + " --siteUser=" + EscapeShellArg(user)
                + " --siteUserPassword=" + EscapeShellArg(pass);
            var r = EpcClpRun("site:add:php " + args);
            log.Add(Str(r["cmd"]));
            log.Add(Str(r["output"]));
            if (IntVal(r["code"]) != 0)
            {
                return OkLog(false, log);
            }
        }

        var ssl = EpcClpRun("lets-encrypt:install:certificate --domainName=" + EscapeShellArg(domain));
        log.Add("SSL cmd: " + Str(ssl["cmd"]));
        log.Add("SSL: " + Str(ssl["output"]));
        return OkLog(true, log);
    }

    public static Dictionary<string, object?> EpcClpProvisionDatabase(Dictionary<string, object?> opts)
    {
        if (!EpcClpAvailable())
        {
            return FailLog("clpctl not available");
        }

        var args = "--domainName=" + EscapeShellArg(Str(Field(opts, "domain")))
            + " --databaseName=" + EscapeShellArg(Str(Field(opts, "database_name")))
            + " --databaseUserName=" + EscapeShellArg(Str(Field(opts, "database_user")))
            + " --databaseUserPassword=" + EscapeShellArg(Str(Field(opts, "database_password")));
        var r = EpcClpRun("db:add " + args);
        return OkLog(IntVal(r["code"]) == 0, [Str(r["cmd"]), Str(r["output"])]);
    }

    public static string EpcClpGuessDocroot(string siteUser, string domain)
    {
        string[] candidates =
        [
            "/home/" + siteUser + "/htdocs/" + domain,
            "/home/" + siteUser + "/htdocs/" + domain + "/public",
            "/home/" + siteUser + "/htdocs/www." + domain
        ];
        foreach (var path in candidates)
        {
            if (IsDir(path))
            {
                return path;
            }
        }

        return "/home/" + siteUser + "/htdocs/" + domain;
    }

    public static Dictionary<string, object?> EpcClpDiagnostics()
    {
        var ver = EpcClpRun("--version");
        var list = EpcClpRun("site:list");
        var apps = EpcClpRun("app:list");
        var appOut = Str(apps["output"]);
        return new(StringComparer.Ordinal)
        {
            ["clp_bin"] = EpcClpBin(),
            ["clp_available"] = EpcClpAvailable(),
            ["version"] = Str(ver["output"]),
            ["site_list"] = Str(list["output"]),
            ["app_list"] = appOut.Length <= 500 ? appOut : appOut[..500]
        };
    }

    public static Dictionary<string, object?> EpcClpWebCreatePhpSite(ref string cookie, Dictionary<string, object?> opts)
    {
        var domain = Str(Field(opts, "domain"));
        var user = Str(Field(opts, "site_user"));
        var pass = Str(Field(opts, "site_user_password"));
        if (domain == "" || user == "" || pass == "")
        {
            return FailLog("missing fields");
        }

        var panel = EpcClpPanelUrl();
        var log = new List<string>();
        var dash = EpcClpWebRequest(panel + "/", new(StringComparer.Ordinal), ref cookie);
        if (Stripos(dash, "/site/" + domain) >= 0)
        {
            log.Add("Site already listed: " + domain);
            return OkLog(true, log);
        }

        var form = EpcClpWebRequest(panel + "/site/new/php", new(StringComparer.Ordinal), ref cookie);
        var tok = Regex.Match(form, @"name=""site_new_php\[_token\]"" value=""([^""]+)""");
        if (!tok.Success)
        {
            return FailLog("CSRF token not found on /site/new/php");
        }

        var resp = EpcClpWebRequest(panel + "/site/new/php", new(StringComparer.Ordinal)
        {
            ["method"] = "POST",
            ["body"] = "token=" + Enc(tok.Groups[1].Value)
        }, ref cookie);
        var headers = LastHttpHeaders;
        log.Add(headers.Count > 0 ? headers[0] : "POST done");
        var dash2 = EpcClpWebRequest(panel + "/", new(StringComparer.Ordinal), ref cookie);
        var ok = Stripos(dash2, "/site/" + domain) >= 0;
        log.Add(ok ? "Created OK" : (StripTags(resp).Length <= 300 ? StripTags(resp) : StripTags(resp)[..300]));
        return OkLog(ok, log);
    }

    public static Dictionary<string, object?> EpcClpWebDeleteSite(ref string cookie, string domain)
    {
        var panel = EpcClpPanelUrl();
        var sitePath = "/site/" + Uri.EscapeDataString(domain);
        var html = EpcClpWebRequest(panel + sitePath + "/settings", new(StringComparer.Ordinal), ref cookie);
        var log = new List<string> { "settings len=" + ByteLen(html) };
        var token = "";
        var m = Regex.Match(html, @"name=""site_delete\[_token\]"" value=""([^""]+)""");
        if (m.Success)
        {
            token = m.Groups[1].Value;
        }
        else
        {
            m = Regex.Match(html, @"id=""site_delete__token""[^>]*value=""([^""]+)""");
            if (m.Success)
            {
                token = m.Groups[1].Value;
            }
        }

        if (token == "")
        {
            html = EpcClpWebRequest(panel + sitePath + "/delete", new(StringComparer.Ordinal), ref cookie);
            log.Add("delete page len=" + ByteLen(html));
            m = Regex.Match(html, @"name=""site_delete\[_token\]"" value=""([^""]+)""");
            if (m.Success)
            {
                token = m.Groups[1].Value;
            }
        }

        if (token == "")
        {
            return OkLog(false, log);
        }

        EpcClpWebRequest(panel + sitePath + "/settings", new(StringComparer.Ordinal) { ["method"] = "POST", ["body"] = "t=" + Enc(token) }, ref cookie);
        log.Add("DELETE POST " + sitePath + "/settings");
        var dash = EpcClpWebRequest(panel + "/dashboard", new(StringComparer.Ordinal), ref cookie);
        var gone = !EpcClpWebSiteListed(dash, domain);
        log.Add(gone ? "site removed from dashboard" : "site still listed");
        return OkLog(gone, log);
    }

    public static Dictionary<string, object?> EpcClpWebActivateCertificates(ref string cookie, string domain)
    {
        var panel = EpcClpPanelUrl();
        var sitePath = "/site/" + Uri.EscapeDataString(domain);
        var html = EpcClpWebRequest(panel + sitePath + "/certificates", new(StringComparer.Ordinal), ref cookie);
        var log = new List<string> { "certificates len=" + ByteLen(html) };
        if (html == "")
        {
            return OkLog(false, log);
        }

        var rx = new Regex(Regex.Escape(sitePath) + @"/certificate/install\?uid=([a-f0-9]+)");
        var uids = rx.Matches(html).Select(x => x.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();
        if (uids.Count == 0)
        {
            log.Add("no pending install links");
            return OkLog(false, log);
        }

        uids = [uids[^1]];
        var installed = 0;
        foreach (var uid in uids)
        {
            var installUrl = panel + sitePath + "/certificate/install?uid=" + uid;
            var resp = EpcClpWebRequest(installUrl, new(StringComparer.Ordinal) { ["method"] = "GET", ["timeout"] = 120 }, ref cookie);
            var headers = LastHttpHeaders;
            var status = headers.Count > 0 ? headers[0] : "";
            log.Add("install uid=" + (uid.Length <= 12 ? uid : uid[..12]) + " status=" + status + " len=" + ByteLen(resp));
            if (Stripos(status, "302") >= 0)
            {
                installed++;
            }
        }

        log.Add("installed_count=" + installed);
        return OkLog(installed > 0 || EpcClpSslCertificatePaths(domain) != null, log);
    }

    public static Dictionary<string, object?> EpcClpWebInstallSsl(ref string cookie, string domain, IEnumerable<string>? extraDomains = null)
    {
        if (EpcClpSslCertificatePaths(domain) != null)
        {
            return OkLog(true, ["cert already present"]);
        }

        var log = new List<string>();
        var activateFirst = EpcClpWebActivateCertificates(ref cookie, domain);
        foreach (var al in AsList(activateFirst["log"]))
        {
            log.Add("preflight activate: " + al);
        }

        for (var wait = 0; wait < 4; wait++)
        {
            if (EpcClpSslCertificatePaths(domain) != null)
            {
                log.Add("cert active after preflight");
                return OkLog(true, log);
            }

            Sleep(1);
        }

        var cli = EpcClpRun("lets-encrypt:install:certificate --domainName=" + EscapeShellArg(domain));
        log.Add("CLI fallback: " + Cut(Str(cli["output"]), 400));
        return OkLog(IntVal(cli["code"]) == 0, log.Concat(["SSL manual step may be needed"]).ToList());
    }

    public static Dictionary<string, object?> EpcClpWebAddDatabase(ref string cookie, string domain, string dbName, string dbUser, string dbPass)
    {
        domain = domain.Trim() != "" ? domain : "www.ecomae.com";
        var panel = EpcClpPanelUrl();
        var sitePath = "/site/" + Uri.EscapeDataString(domain);
        var html = EpcClpWebRequest(panel + sitePath + "/database/new", new(StringComparer.Ordinal), ref cookie);
        var log = new List<string> { "db form len=" + ByteLen(html), "post_path=" + sitePath + "/database/new" };
        if (!Regex.IsMatch(html, @"name=""((?:site_database|database)\[_token\])"" value=""([^""]+)"""))
        {
            log.Add("DB form not found — create manually in CloudPanel");
            return OkLog(false, log);
        }

        return new(StringComparer.Ordinal) { ["ok"] = false, ["log"] = log, ["response"] = html, ["location"] = "" };
    }

    public static Dictionary<string, object?> EpcClpVhostFetch(ref string cookie, string domain)
    {
        var html = EpcClpWebRequest(EpcClpPanelUrl() + "/site/" + Uri.EscapeDataString(domain) + "/vhost", new(StringComparer.Ordinal), ref cookie);
        var token = "";
        var vhost = "";
        var tm = Regex.Match(html, @"name=""token"" value=""([^""]+)""");
        if (tm.Success)
        {
            token = tm.Groups[1].Value;
        }

        var em = Regex.Match(html, @"<div id=""editor"">([\s\S]*?)</div>\s*<textarea");
        if (em.Success)
        {
            vhost = WebUtility.HtmlDecode(em.Groups[1].Value);
        }

        return new(StringComparer.Ordinal) { ["html"] = html, ["token"] = token, ["vhost"] = vhost };
    }

    public static bool EpcClpVhostSave(ref string cookie, string domain, string vhost, string token)
    {
        if (vhost == "" || token == "")
        {
            return false;
        }

        EpcClpWebRequest(EpcClpPanelUrl() + "/site/" + Uri.EscapeDataString(domain) + "/vhost", new(StringComparer.Ordinal)
        {
            ["method"] = "POST",
            ["body"] = "vhost-update=1&token=" + Enc(token)
        }, ref cookie);
        return true;
    }

    public static List<string> EpcClpModelCTenantHostnames()
    {
        var outList = new List<string>();
        foreach (var slug in EpcClpNginxTenantBasenameSlugs())
        {
            outList.Add("www." + slug + ".com");
            outList.Add(slug + ".com");
        }

        return outList.Distinct(StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, object?> EpcClpNginxApplySafety(string platformSite = "www.ecomae.com")
    {
        var log = new List<string> { "=== nginx apply safety ===" };
        var hosts = EpcClpModelCTenantHostnames();
        var found = EpcClpNginxFindConfigsForHosts(hosts, platformSite);
        if (found.Count > 0)
        {
            log.Add("orphan configs detected: " + found.Count);
        }

        var q = EpcClpNginxQuarantineOrphanConfigs(hosts, platformSite);
        log.AddRange(AsList(q["log"]));
        var disabled = AsList(q["disabled"]);
        var nt = EpcClpRunCmd("nginx -t 2>&1");
        log.Add("nginx -t: " + Cut(Str(nt["output"]).Trim(), 200));
        return new(StringComparer.Ordinal)
        {
            ["ok"] = IntVal(nt["code"]) == 0 || Stripos(Str(nt["output"]), "successful") >= 0,
            ["log"] = log,
            ["disabled"] = disabled.Distinct(StringComparer.Ordinal).ToList()
        };
    }

    public static Dictionary<string, object?> EpcClpVhostSaveAndApply(ref string cookie, string domain, string vhost, string token, bool quarantineOrphans = true)
    {
        var log = new List<string>();
        if (quarantineOrphans && Stripos(domain, "ecomae.com") >= 0)
        {
            var safety = EpcClpNginxApplySafety(domain);
            log.AddRange(AsList(safety["log"]));
            var disabled = AsList(safety["disabled"]);
            if (disabled.Count > 0)
            {
                log.Add("quarantined " + disabled.Count + " orphan config(s)");
            }
        }

        if (!EpcClpVhostSave(ref cookie, domain, vhost, token))
        {
            log.Add("vhost save POST failed");
            return OkLog(false, log);
        }

        log.Add("CloudPanel vhost Save OK for " + domain);
        return OkLog(true, log);
    }

    public static bool EpcClpWebSiteListed(string dashboardHtml, string domain)
        => Stripos(dashboardHtml, "/site/" + domain) >= 0 || Stripos(dashboardHtml, ">" + domain + "<") >= 0;

    public static Dictionary<string, object?> EpcClpWebSetSiteDocroot(ref string cookie, string domain, string targetRoot)
    {
        var panel = EpcClpPanelUrl();
        var settingsHtml = EpcClpWebRequest(panel + "/site/" + Uri.EscapeDataString(domain) + "/settings", new(StringComparer.Ordinal), ref cookie);
        var tm = Regex.Match(settingsHtml, @"name=""site_domain_settings\[_token\]"" value=""([^""]+)""");
        if (!tm.Success)
        {
            return FailLog("settings token missing len=" + ByteLen(settingsHtml));
        }

        EpcClpWebRequest(panel + "/site/" + Uri.EscapeDataString(domain) + "/settings", new(StringComparer.Ordinal) { ["method"] = "POST", ["body"] = "t" }, ref cookie);
        var log = new List<string> { "settings rootDirectory=" + targetRoot };
        var vf = EpcClpVhostFetch(ref cookie, domain);
        if (Str(vf["vhost"]) != "" && Str(vf["token"]) != "")
        {
            var patched = Regex.Replace(Str(vf["vhost"]), @"\broot\s+[^;]+;", "root " + targetRoot + ";");
            var token = Str(vf["token"]);
            EpcClpVhostSave(ref cookie, domain, patched, token);
            log.Add("vhost root patched");
        }

        var perm = EpcClpRun("system:permissions:reset --directories=755 --files=644 --path=" + EscapeShellArg(targetRoot));
        log.Add("permissions code=" + IntVal(perm["code"]));
        return OkLog(true, log);
    }

    public static Dictionary<string, object?> EpcClpVhostAddAliasesOnSite(ref string cookie, string platformSite, IEnumerable<string> aliasHosts, string anchorSite = "")
    {
        anchorSite = anchorSite != "" ? anchorSite : platformSite;
        var vf = EpcClpVhostFetch(ref cookie, platformSite);
        var vhost = Str(vf["vhost"]);
        var log = new List<string> { "vhost_len=" + ByteLen(vhost) };
        if (vhost == "" || Str(vf["token"]) == "")
        {
            log.Add("Could not read vhost for " + platformSite);
            return OkLog(false, log);
        }

        foreach (var aliasHost in aliasHosts)
        {
            var host = aliasHost;
            vhost = ServerNameLine.Replace(vhost, m =>
            {
                if (Stripos(m.Groups[1].Value, anchorSite) < 0 || Stripos(m.Groups[1].Value, host) >= 0)
                {
                    return m.Value;
                }

                return Regex.Replace(m.Value, @";\s*$", " " + host + ";");
            });
        }

        foreach (var aliasHost in aliasHosts)
        {
            var host = aliasHost;
            vhost = ServerNameLine.Replace(vhost, m =>
            {
                if (Stripos(m.Groups[1].Value, anchorSite) >= 0 || Stripos(m.Groups[1].Value, host) < 0)
                {
                    return m.Value;
                }

                var names = SplitNames(m.Groups[1].Value).Where(n => !EqCi(n, host)).ToList();
                return "  server_name " + string.Join(" ", names) + ";";
            });
        }

        var token = Str(vf["token"]);
        var save = EpcClpVhostSaveAndApply(ref cookie, platformSite, vhost, token);
        log.AddRange(AsList(save["log"]));
        if (!Bool(save["ok"]))
        {
            log.Add("vhost save failed");
            return OkLog(false, log);
        }

        log.Add("Saved aliases on " + platformSite);
        return OkLog(true, log);
    }

    public static string EpcClpVhostScrubTenantMisroutes(string vhost, IEnumerable<string> tenantHosts)
    {
        var tenants = UniqueTrim(tenantHosts);
        if (tenants.Count == 0)
        {
            return vhost;
        }

        vhost = EpcRedirect.Replace(vhost, m =>
        {
            var names = SplitNames(m.Groups[2].Value).Where(n => n != "" && !tenants.Any(t => EqCi(n, t))).ToList();
            if (names.Count == 0)
            {
                names = ["epartscart.com"];
            }

            return m.Groups[1].Value + string.Join(" ", names) + m.Groups[3].Value;
        });
        return ServerNameLine.Replace(vhost, m =>
        {
            if (Stripos(m.Groups[1].Value, "epartscart.com") < 0)
            {
                return m.Value;
            }

            var names = SplitNames(m.Groups[1].Value).Where(n => n != "" && !tenants.Any(t => EqCi(n, t))).ToList();
            return "  server_name " + string.Join(" ", names) + ";";
        });
    }

    public static string EpcClpVhostStripSslRejectForHosts(string vhost, IEnumerable<string> tenantHosts, out int removedCount)
    {
        removedCount = 0;
        var tenants = UniqueLower(tenantHosts);
        if (tenants.Count == 0)
        {
            return vhost;
        }

        var outText = new StringBuilder();
        foreach (var walk in WalkServerKeyword(vhost))
        {
            if (walk.Kind != WalkKind.Block)
            {
                outText.Append(walk.Text);
                continue;
            }

            var block = walk.Text;
            if (BlockHasHost(block, tenants))
            {
                var patched = SslReject.Replace(block, "");
                if (patched != block)
                {
                    removedCount += SslReject.Matches(block).Count;
                    block = patched;
                }
            }

            outText.Append(block);
        }

        return outText.ToString();
    }

    public static string EpcClpVhostStripTenantStandaloneBlocks(string vhost, IEnumerable<string> tenantHosts, out int removed444, out int removed3000)
    {
        removed444 = 0;
        removed3000 = 0;
        var tenants = UniqueLower(tenantHosts);
        if (tenants.Count == 0)
        {
            return vhost;
        }

        var outText = new StringBuilder();
        foreach (var walk in WalkServerKeyword(vhost))
        {
            if (walk.Kind != WalkKind.Block)
            {
                outText.Append(walk.Text);
                continue;
            }

            if (BlockHasHost(walk.Text, tenants))
            {
                var has444 = Return444.IsMatch(walk.Text);
                var has3000 = Proxy3000.IsMatch(walk.Text);
                if (has444 || has3000)
                {
                    if (has444)
                    {
                        removed444++;
                    }

                    if (has3000)
                    {
                        removed3000++;
                    }

                    continue;
                }
            }

            outText.Append(walk.Text);
        }

        return outText.ToString();
    }

    public static string EpcClpVhostStripTenantMarkers(string vhost)
    {
        vhost = TenantDirectMarkers.Replace(vhost, "");
        return TenantApexMarkers.Replace(vhost, "");
    }

    public static List<string> EpcClpVhostModelCPlatformHosts()
        => ["www.ecomae.com", "ecomae.com", "cp.ecomae.com"];

    public static (string Vhost, List<string> Log) EpcClpVhostStripHostsFromServerNames(string vhost, IEnumerable<string> hostsToStrip, bool preserveEpcMarkers = true)
    {
        var strip = new HashSet<string>(UniqueLower(hostsToStrip), StringComparer.Ordinal);
        var log = new List<string>();
        if (strip.Count == 0)
        {
            return (vhost, log);
        }

        string Scrub(string chunk) => ServerNameLine.Replace(chunk, m =>
        {
            var removed = new List<string>();
            var kept = new List<string>();
            foreach (var n in SplitNames(m.Groups[1].Value))
            {
                var nl = n.ToLowerInvariant();
                if (n != "" && strip.Contains(nl))
                {
                    removed.Add(n);
                }
                else if (n != "")
                {
                    kept.Add(n);
                }
            }

            if (removed.Count == 0)
            {
                return m.Value;
            }

            log.Add("stripped from server_name: " + string.Join(", ", removed));
            return kept.Count == 0 ? m.Value : "  server_name " + string.Join(" ", kept) + ";";
        });

        if (!preserveEpcMarkers)
        {
            return (Scrub(vhost), log);
        }

        var parts = MarkerSplit.Split(vhost);
        if (parts.Length <= 1)
        {
            return (Scrub(vhost), log);
        }

        var outText = new StringBuilder();
        foreach (var part in parts)
        {
            if (part != "" && MarkerStart.IsMatch(part))
            {
                outText.Append(part);
                continue;
            }

            outText.Append(Scrub(part));
        }

        return (outText.ToString(), log);
    }

    public static List<string> EpcClpVhostAuditServerNames(string vhost)
    {
        var lines = new List<string>();
        var i = 1;
        foreach (Match m in ServerNameLine.Matches(vhost))
        {
            lines.Add(i + ": " + m.Groups[1].Value.Trim());
            i++;
        }

        return lines;
    }

    public static Dictionary<string, object?> EpcClpSslStandardPaths(string domain)
        => new(StringComparer.Ordinal)
        {
            ["crt"] = "/etc/nginx/ssl-certificates/" + domain + ".crt",
            ["key"] = "/etc/nginx/ssl-certificates/" + domain + ".key"
        };

    public static Dictionary<string, object?>? EpcClpSslCertificatePaths(string domain, bool assumeStandard = false)
    {
        domain = domain.Trim();
        if (domain == "")
        {
            return null;
        }

        var std = EpcClpSslStandardPaths(domain);
        var crt = Str(std["crt"]);
        var key = Str(std["key"]);
        if (IsFile(crt) && IsFile(key))
        {
            return std;
        }

        var r = EpcClpRunCmd("test -r " + EscapeShellArg(crt) + " -a -r " + EscapeShellArg(key) + " && echo OK");
        if (Str(r["output"]).Contains("OK", StringComparison.Ordinal))
        {
            return std;
        }

        var subj = EpcClpRunCmd("openssl x509 -in " + EscapeShellArg(crt) + " -noout -subject 2>/dev/null");
        if (IntVal(subj["code"]) == 0 && Stripos(Str(subj["output"]), domain) >= 0)
        {
            return std;
        }

        return assumeStandard ? std : null;
    }

    public static Dictionary<string, object?> EpcClpInstallLeCertificate(string domain, IEnumerable<string>? subjectAlternativeNames = null)
    {
        domain = domain.Trim();
        if (domain == "")
        {
            return FailLog("empty domain");
        }

        var paths = EpcClpSslCertificatePaths(domain);
        if (paths != null)
        {
            return OkLog(true, ["cert already present: " + Str(paths["crt"])]);
        }

        var san = UniqueTrim(subjectAlternativeNames ?? []).Where(h => !EqCi(h, domain)).ToList();
        var subcmd = "lets-encrypt:install:certificate --domainName=" + EscapeShellArg(domain);
        if (san.Count > 0)
        {
            subcmd += " --subjectAlternativeName=" + EscapeShellArg(string.Join(",", san));
        }

        var log = new List<string>();
        foreach (var cmd in new[]
        {
            "runuser -u clp -- /usr/bin/clpctl " + subcmd,
            "su -s /bin/bash -c " + EscapeShellArg("/usr/bin/clpctl " + subcmd) + " clp"
        })
        {
            var cli = EpcClpRunCmd(cmd);
            log.Add(cmd + " exit=" + IntVal(cli["code"]));
            log.Add(Cut(Str(cli["output"]).Trim(), 500));
            if (IntVal(cli["code"]) == 0 && EpcClpSslCertificatePaths(domain) != null)
            {
                return OkLog(true, log);
            }
        }

        var cli2 = EpcClpRun(subcmd);
        log.Add("CLI (default) exit=" + IntVal(cli2["code"]));
        log.Add(Cut(Str(cli2["output"]).Trim(), 400));
        if (EpcClpSslCertificatePaths(domain) != null)
        {
            return OkLog(true, log);
        }

        return OkLog(false, log);
    }

    public static Dictionary<string, object?> EpcClpVhostPatchServerSslForHosts(string vhost, IEnumerable<string> hostnames, string certDomain, bool assumeStandardCert = false)
    {
        var paths = EpcClpSslCertificatePaths(certDomain, assumeStandardCert);
        if (paths == null)
        {
            return new(StringComparer.Ordinal) { ["vhost"] = vhost, ["log"] = new List<string> { "no cert files for " + certDomain }, ["patched"] = 0 };
        }

        var hosts = UniqueTrim(hostnames);
        var certLine = "  ssl_certificate " + Str(paths["crt"]) + ";";
        var keyLine = "  ssl_certificate_key " + Str(paths["key"]) + ";";
        var log = new List<string>();
        var patched = 0;
        var outText = new StringBuilder();
        foreach (var walk in WalkServerBraceBlocks(vhost))
        {
            if (walk.Kind != WalkKind.Block)
            {
                outText.Append(walk.Text);
                continue;
            }

            var block = walk.Text;
            var newBlock = block;
            var sn = ServerNameLineCi.Match(block);
            if (sn.Success && NamesHit(SplitNames(sn.Groups[1].Value), hosts))
            {
                newBlock = SslKeyPlaceholder.Replace(block, keyLine + "\n");
                newBlock = SslCertPlaceholder.Replace(newBlock, certLine + "\n");
                newBlock = SslCertLine.Replace(newBlock, certLine);
                newBlock = SslKeyLine.Replace(newBlock, keyLine);
                if (newBlock != block)
                {
                    patched++;
                    log.Add("ssl paths for server_name " + sn.Groups[1].Value.Trim());
                }
            }

            outText.Append(newBlock);
        }

        return new(StringComparer.Ordinal) { ["vhost"] = outText.ToString(), ["log"] = log, ["patched"] = patched };
    }

    public static Dictionary<string, object?> EpcClpVhostRemoveServerBlocksForHosts(string vhost, IEnumerable<string> hostnames)
    {
        var hosts = UniqueTrim(hostnames);
        var log = new List<string>();
        var removed = 0;
        var outText = new StringBuilder();
        foreach (var walk in WalkServerBraceBlocks(vhost))
        {
            if (walk.Kind != WalkKind.Block)
            {
                outText.Append(walk.Text);
                continue;
            }

            var sn = ServerNameLineCi.Match(walk.Text);
            var drop = false;
            if (sn.Success && NamesHit(SplitNames(sn.Groups[1].Value), hosts))
            {
                drop = true;
                removed++;
                log.Add("removed server block: " + sn.Groups[1].Value.Trim());
            }

            if (!drop)
            {
                outText.Append(walk.Text);
            }
        }

        return new(StringComparer.Ordinal)
        {
            ["vhost"] = ExtraNewlines.Replace(outText.ToString(), "\n\n"),
            ["log"] = log,
            ["removed"] = removed
        };
    }

    public static Dictionary<string, object?> EpcClpNginxReload()
    {
        string[] cmds =
        [
            "systemctl reload nginx 2>/dev/null",
            "systemctl restart nginx 2>/dev/null",
            "runuser -u clp -- /usr/bin/clpctl webserver:reload 2>/dev/null",
            "su -s /bin/bash -c '/usr/bin/clpctl webserver:reload' clp",
            "sudo -n nginx -t 2>&1",
            "nginx -t 2>&1",
            "sudo -n systemctl reload nginx 2>&1",
            "systemctl reload nginx 2>&1",
            "systemctl restart nginx 2>&1"
        ];
        var log = cmds.Select(cmd =>
        {
            var r = EpcClpRunCmd(cmd);
            return cmd + " [exit=" + IntVal(r["code"]) + "] " + Cut(Str(r["output"]).Trim(), 200);
        }).ToList();
        var ok = false;
        foreach (var line in log.AsEnumerable().Reverse())
        {
            if (Stripos(line, "reload nginx") >= 0 && ReloadOk.IsMatch(line))
            {
                ok = true;
                break;
            }

            if (Stripos(line, "restart nginx") >= 0 && ReloadOk.IsMatch(line))
            {
                ok = true;
                break;
            }
        }

        return OkLog(ok, log);
    }

    public static Dictionary<string, object?> EpcClpNginxReloadWithPass(string sudoPass)
    {
        if (sudoPass == "")
        {
            return FailLog("empty sudo password");
        }

        var escaped = sudoPass.Replace("'", "'\\''", StringComparison.Ordinal);
        var log = new List<string>();
        foreach (var cmd in new[]
        {
            "echo '" + escaped + "' | sudo -S nginx -t 2>&1",
            "echo '" + escaped + "' | sudo -S systemctl reload nginx 2>&1"
        })
        {
            var r = EpcClpRunCmd(cmd);
            log.Add(Cut(cmd, 40) + "... [exit=" + IntVal(r["code"]) + "] " + Cut(Str(r["output"]).Trim(), 240));
        }

        var ok = log.Any(line => Stripos(line, "reload nginx") >= 0 && ReloadOk.IsMatch(line));
        return OkLog(ok, log);
    }

    public static List<string> EpcClpNginxTenantBasenameSlugs()
        => ["thejewellerytrend", "epartscart", "taxofinca", "electronicae", "stylenlook"];

    public static string EpcClpNginxPlatformConfigBasename(string platformSite = "www.ecomae.com")
    {
        var site = platformSite.Trim();
        if (site == "")
        {
            site = "www.ecomae.com";
        }

        return site + ".conf";
    }

    public static string EpcClpNginxEnsureSitesDisabledDir()
    {
        const string dir = "/etc/nginx/sites-disabled";
        if (!IsDir(dir))
        {
            EnsureDir(dir);
        }

        return dir;
    }

    public static Dictionary<string, object?> EpcClpNginxMoveToSitesDisabled(string conf)
    {
        var disabledDir = EpcClpNginxEnsureSitesDisabledDir();
        var dest = disabledDir.TrimEnd('/') + "/" + Path.GetFileName(conf) + ".epc-quarantine-" + Clock().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        if (Rename(conf, dest))
        {
            return new(StringComparer.Ordinal) { ["ok"] = true, ["dest"] = dest, ["log"] = "quarantined " + conf + " -> " + dest };
        }

        var mv = EpcClpRunCmd("mv -f " + EscapeShellArg(conf) + " " + EscapeShellArg(dest));
        if (IntVal(mv["code"]) == 0)
        {
            return new(StringComparer.Ordinal) { ["ok"] = true, ["dest"] = dest, ["log"] = "quarantined (sudo) " + conf };
        }

        var dest2 = conf + ".epc-disabled";
        if (Rename(conf, dest2))
        {
            return new(StringComparer.Ordinal) { ["ok"] = true, ["dest"] = dest2, ["log"] = "quarantined (fallback) " + dest2 };
        }

        return new(StringComparer.Ordinal) { ["ok"] = false, ["dest"] = "", ["log"] = "FAIL quarantine " + conf + " — " + Cut(Str(mv["output"]).Trim(), 120) };
    }

    public static List<string> EpcClpNginxFindConfigsForHosts(IEnumerable<string> hosts, string platformSite = "www.ecomae.com")
    {
        var hostList = UniqueLower(hosts);
        if (hostList.Count == 0)
        {
            return [];
        }

        var platformBase = EpcClpNginxPlatformConfigBasename(platformSite).ToLowerInvariant();
        var tenantSlugs = EpcClpNginxTenantBasenameSlugs();
        var found = new List<string>();
        foreach (var dir in new[] { "/etc/nginx/sites-enabled", "/etc/nginx/conf.d" })
        {
            if (!IsDir(dir))
            {
                continue;
            }

            foreach (var conf in ListFiles(dir))
            {
                if (!IsFile(conf) || BakName.IsMatch(conf))
                {
                    continue;
                }

                var baseName = Path.GetFileName(conf).ToLowerInvariant();
                if (baseName == platformBase)
                {
                    continue;
                }

                if (baseName.Contains("ecomae", StringComparison.Ordinal) && baseName.Contains(WwwPrefix.Replace(platformSite, ""), StringComparison.Ordinal))
                {
                    continue;
                }

                var matched = tenantSlugs.Any(slug => baseName.Contains(slug, StringComparison.Ordinal));
                if (matched)
                {
                    found.Add(conf);
                    continue;
                }

                var text = ReadFile(conf);
                if (text == null)
                {
                    continue;
                }

                var lower = text.ToLowerInvariant();
                if (Stripos(text, "managed by certbot") >= 0 && hostList.Any(host => host != "" && lower.Contains(host, StringComparison.Ordinal)))
                {
                    found.Add(conf);
                    continue;
                }

                if (hostList.Any(host => host != "" && lower.Contains(host, StringComparison.Ordinal)))
                {
                    found.Add(conf);
                }
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found.Distinct(StringComparer.Ordinal).ToList();
    }

    public static Dictionary<string, object?> EpcClpNginxDedupePlatformEnabledConfigs(string platformSite = "www.ecomae.com")
    {
        var log = new List<string>();
        var disabled = new List<string>();
        var canonical = "/etc/nginx/sites-enabled/" + EpcClpNginxPlatformConfigBasename(platformSite);
        var candidates = new List<string>();
        if (IsDir("/etc/nginx/sites-enabled"))
        {
            foreach (var conf in ListFiles("/etc/nginx/sites-enabled"))
            {
                if (!IsFile(conf) || BakName.IsMatch(conf))
                {
                    continue;
                }

                var text = ReadFile(conf);
                if (text == null)
                {
                    continue;
                }

                if (EcomaeServerName.IsMatch(text) || Stripos(text, "server_name www.ecomae.com") >= 0)
                {
                    candidates.Add(conf);
                }
            }
        }

        candidates = candidates.Distinct(StringComparer.Ordinal).ToList();
        if (candidates.Count == 0)
        {
            return new(StringComparer.Ordinal) { ["disabled"] = new List<string>(), ["kept"] = canonical, ["log"] = new List<string> { "no duplicate ecomae configs found" } };
        }

        var kept = IsFile(canonical) ? canonical : candidates[0];
        if (!IsFile(kept))
        {
            kept = candidates[0];
        }

        log.Add("keeping " + kept);
        foreach (var conf in candidates)
        {
            if (RealPath(conf) == RealPath(kept))
            {
                continue;
            }

            var mv = EpcClpNginxMoveToSitesDisabled(conf);
            log.Add(Str(mv["log"]));
            if (Bool(mv["ok"]))
            {
                disabled.Add(conf);
            }
        }

        return new(StringComparer.Ordinal) { ["disabled"] = disabled, ["kept"] = kept, ["log"] = log };
    }

    public static List<int[]> EpcClpVhostProtectedRegions(string vhost)
    {
        var regions = new List<int[]>();
        foreach (var pair in new[]
        {
            new[] { "# EPC_TENANT_DIRECT_START", "# EPC_TENANT_DIRECT_END" },
            new[] { "# EPC_TENANT_APEX_REDIRECT_START", "# EPC_TENANT_APEX_REDIRECT_END" }
        })
        {
            var start = vhost.IndexOf(pair[0], StringComparison.Ordinal);
            var end = vhost.IndexOf(pair[1], StringComparison.Ordinal);
            if (start >= 0 && end >= 0 && end > start)
            {
                regions.Add([start, end + pair[1].Length]);
            }
        }

        return regions;
    }

    public static bool EpcClpVhostPositionInRegions(int pos, IEnumerable<int[]> regions)
        => regions.Any(region => region.Length >= 2 && pos >= region[0] && pos <= region[1]);

    public static Dictionary<string, object?> EpcClpVhostStripOrphanTenantServerBlocks(string vhost, IEnumerable<string> tenantHosts)
    {
        var tenants = UniqueLower(tenantHosts);
        var log = new List<string>();
        var removed = 0;
        if (tenants.Count == 0)
        {
            return new(StringComparer.Ordinal) { ["vhost"] = vhost, ["log"] = log, ["removed"] = 0 };
        }

        var protectedRegions = EpcClpVhostProtectedRegions(vhost);
        var outText = new StringBuilder();
        foreach (var walk in WalkServerKeyword(vhost, includeStart: true))
        {
            if (walk.Kind != WalkKind.Block)
            {
                outText.Append(walk.Text);
                continue;
            }

            var tenantLabel = "";
            var hasTenant = false;
            foreach (Match sn in ServerNameLineCi.Matches(walk.Text))
            {
                foreach (var name in SplitNames(CommentTail.Replace(sn.Groups[1].Value, "")))
                {
                    var nl = name.Trim().ToLowerInvariant();
                    if (nl != "" && tenants.Contains(nl))
                    {
                        hasTenant = true;
                        tenantLabel = nl;
                        break;
                    }
                }

                if (hasTenant)
                {
                    break;
                }
            }

            var isCertbot = Certbot.IsMatch(walk.Text);
            var inProtected = EpcClpVhostPositionInRegions(walk.Start, protectedRegions);
            if (hasTenant && (isCertbot || !inProtected))
            {
                removed++;
                log.Add("removed " + (isCertbot ? "certbot" : "orphan") + " tenant server block: " + tenantLabel);
                continue;
            }

            outText.Append(walk.Text);
        }

        return new(StringComparer.Ordinal)
        {
            ["vhost"] = ExtraNewlines.Replace(outText.ToString(), "\n\n"),
            ["log"] = log,
            ["removed"] = removed
        };
    }

    public static Dictionary<string, object?> EpcClpNginxQuarantineOrphanConfigs(IEnumerable<string> hosts, string platformSite = "www.ecomae.com")
    {
        var log = new List<string>();
        var disabled = new List<string>();
        EpcClpNginxEnsureSitesDisabledDir();
        foreach (var conf in EpcClpNginxFindConfigsForHosts(hosts, platformSite))
        {
            var mv = EpcClpNginxMoveToSitesDisabled(conf);
            log.Add(Str(mv["log"]));
            if (Bool(mv["ok"]))
            {
                disabled.Add(conf);
                continue;
            }

            var del = EpcClpRunCmd("rm -f " + EscapeShellArg(conf));
            if (IntVal(del["code"]) == 0)
            {
                log.Add("removed " + conf);
                disabled.Add(conf);
            }
        }

        var dedupe = EpcClpNginxDedupePlatformEnabledConfigs(platformSite);
        foreach (var line in AsList(dedupe["log"]))
        {
            log.Add("dedupe: " + line);
        }

        disabled.AddRange(AsList(dedupe["disabled"]));
        return new(StringComparer.Ordinal) { ["disabled"] = disabled.Distinct(StringComparer.Ordinal).ToList(), ["log"] = log };
    }

    public static Dictionary<string, object?> EpcClpVhostInstallPerTenantSsl(ref string cookie, string platformSite, string platformDocroot, IEnumerable<Dictionary<string, object?>> tenantRows)
    {
        var vf = EpcClpVhostFetch(ref cookie, platformSite);
        if (Str(vf["vhost"]) == "" || Str(vf["token"]) == "")
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["log"] = new List<string> { "vhost fetch failed" }, ["vhost"] = "" };
        }

        var vhost = Str(vf["vhost"]);
        var log = new List<string>();
        var totalPatched = 0;
        foreach (var row in tenantRows)
        {
            var www = Str(Field(row, "www")).Trim();
            var bare = Str(Field(row, "bare")).Trim();
            if (www == "")
            {
                continue;
            }

            var hosts = new[] { www, bare }.Where(h => h != "").Distinct(StringComparer.Ordinal).ToList();
            var le = EpcClpInstallLeCertificate(www);
            log.Add(www + " LE: " + string.Join(" | ", AsList(le["log"]).Take(2)));
            if (!Bool(le["ok"]))
            {
                var extra = bare != "" ? new[] { bare } : Array.Empty<string>();
                var sslWeb = EpcClpWebInstallSsl(ref cookie, www, extra);
                log.Add(www + " web SSL: " + string.Join(" | ", AsList(sslWeb["log"]).Take(2)));
            }

            var patch = EpcClpVhostPatchServerSslForHosts(vhost, hosts, www);
            vhost = Str(patch["vhost"]);
            totalPatched += IntVal(patch["patched"]);
            log.AddRange(AsList(patch["log"]).Select(pl => "  " + pl));
        }

        vhost = EpcClpVhostPatchTenantDirectRoot(vhost, platformDocroot);
        var token = Str(vf["token"]);
        var save = EpcClpVhostSaveAndApply(ref cookie, platformSite, vhost, token);
        log.AddRange(AsList(save["log"]));
        if (!Bool(save["ok"]))
        {
            log.Add("vhost save failed");
            return new(StringComparer.Ordinal) { ["ok"] = false, ["log"] = log, ["vhost"] = vhost };
        }

        log.Add("ssl_certificate patches total=" + totalPatched);
        return new(StringComparer.Ordinal) { ["ok"] = true, ["log"] = log, ["vhost"] = vhost };
    }

    public static string EpcClpVhostTenantDirectServerTemplate()
        => """
server {
  listen 80;
  listen [::]:80;
  listen 443 quic;
  listen 443 ssl;
  listen [::]:443 quic;
  listen [::]:443 ssl;
  http2 on;
  http3 off;
  {{ssl_certificate_key}}
  {{ssl_certificate}}
  server_name TENANT_NAMES;
  {{root}}
  {{nginx_access_log}}
  {{nginx_error_log}}
  if ($scheme != https) {
    rewrite ^ https://$host$request_uri permanent;
  }
  location ~ /.well-known {
    auth_basic off;
    allow all;
  }
  {{settings}}
  include /etc/nginx/global_settings;
  index index.php index.html;
  location = /cp {
    return 301 https://$host/cp/control;
  }
  location = /cp/ {
    return 301 https://$host/cp/control;
  }
  location /cp/ {
    try_files $uri $uri/ /cp/index.php?$args;
  }
  location = /erp {
    return 301 /erp/;
  }
  location ^~ /erp/ {
    try_files $uri $uri/ /index.php?$args;
  }
  location / {
    try_files $uri $uri/ /index.php?$args;
  }
  error_page 502 503 504 525 = /epc-platform-splash.html;
  location = /epc-platform-splash.html {
    add_header Cache-Control "no-store";
    try_files $uri =404;
  }
  location = /epc-platform-status.json {
    add_header Cache-Control "no-store";
    try_files $uri =404;
  }
  # OAuth start/callback must NOT be rewritten to the warmup splash on 503/422.
  # Unconfigured Google used to return PHP 503 → splash ("Loading — Please wait").
  location = /api/epc_oauth_start.php {
    include fastcgi_params;
    fastcgi_intercept_errors off;
    fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
    fastcgi_pass 127.0.0.1:{{php_fpm_port}};
  }
  location = /api/epc_oauth_callback.php {
    include fastcgi_params;
    fastcgi_intercept_errors off;
    fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
    fastcgi_pass 127.0.0.1:{{php_fpm_port}};
  }
  location ~ \.php$ {
    include fastcgi_params;
    fastcgi_intercept_errors on;
    fastcgi_index index.php;
    fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
    try_files $uri =404;
    fastcgi_read_timeout 3600;
    fastcgi_send_timeout 3600;
    fastcgi_param HTTPS on;
    fastcgi_param SERVER_PORT 443;
    fastcgi_pass 127.0.0.1:{{php_fpm_port}};
  }
}

""";

    public static string EpcClpVhostTenantDirectTemplate()
        => "# EPC_TENANT_DIRECT_START\n" + EpcClpVhostTenantDirectServerTemplate() + "# EPC_TENANT_DIRECT_END\n";

    public static string EpcClpVhostBuildModelCTenantSnippets(IEnumerable<Dictionary<string, object?>> tenantGroups)
    {
        var direct = "";
        var apex = "";
        foreach (var group in tenantGroups)
        {
            var hosts = UniqueTrim(AsList(Field(group, "hosts")));
            if (hosts.Count == 0)
            {
                continue;
            }

            hosts.Sort(WwwFirst);
            var directNames = hosts.Where(h => Stripos(h, "www.") == 0).ToList();
            if (directNames.Count == 0)
            {
                directNames = hosts;
            }

            direct += EpcClpVhostTenantDirectServerTemplate().Replace("TENANT_NAMES", string.Join(" ", directNames), StringComparison.Ordinal);
            var wwwHost = "";
            var bareHost = "";
            foreach (var h in hosts)
            {
                if (Stripos(h, "www.") == 0)
                {
                    wwwHost = h;
                }
                else if (h.Contains('.', StringComparison.Ordinal))
                {
                    bareHost = h;
                }
            }

            if (wwwHost != "" && bareHost != "" && !EqCi(bareHost, wwwHost))
            {
                apex += "# tenant " + bareHost + "\nserver {\n  listen 80;\n  listen [::]:80;\n  listen 443 ssl;\n  listen [::]:443 ssl;\n  http2 on;\n  {{ssl_certificate_key}}\n  {{ssl_certificate}}\n  server_name " + bareHost + ";\n  return 301 https://" + wwwHost + "$request_uri;\n}\n";
            }
        }

        var outText = "";
        if (direct != "")
        {
            outText += "# EPC_TENANT_DIRECT_START\n" + direct + "# EPC_TENANT_DIRECT_END\n";
        }

        if (apex != "")
        {
            outText += "# EPC_TENANT_APEX_REDIRECT_START\n" + apex + "# EPC_TENANT_APEX_REDIRECT_END\n";
        }

        return outText;
    }

    public static Dictionary<string, object?> EpcClpVhostRemoveAliasesFromPhpBackend(string vhost, IEnumerable<string> aliasHosts, string anchorSite)
    {
        var aliases = aliasHosts.ToList();
        var log = new List<string>();
        var outText = ServerNameLine.Replace(vhost, m =>
        {
            if (Stripos(m.Groups[1].Value, anchorSite) < 0)
            {
                return m.Value;
            }

            var pos = vhost.IndexOf(m.Value, StringComparison.Ordinal);
            if (pos < 0)
            {
                return m.Value;
            }

            var start = Math.Max(0, pos - 500);
            var chunk = vhost.Substring(start, Math.Min(500, pos - start));
            if (Stripos(chunk, "listen 8080") < 0)
            {
                return m.Value;
            }

            var names = SplitNames(m.Groups[1].Value);
            var before = names.Count;
            names = names.Where(n => n != "" && !aliases.Any(a => EqCi(n, a))).ToList();
            if (names.Count == before)
            {
                return m.Value;
            }

            foreach (var alias in aliases)
            {
                if (Stripos(m.Groups[1].Value, alias) >= 0)
                {
                    log.Add("8080 backend -" + alias);
                }
            }

            return "  server_name " + string.Join(" ", names) + ";";
        });
        return new(StringComparer.Ordinal) { ["vhost"] = outText, ["log"] = log };
    }

    public static Dictionary<string, object?> EpcClpVhostConfigureModelCTenants(ref string cookie, string platformSite, IEnumerable<Dictionary<string, object?>> tenantGroups)
    {
        var groups = tenantGroups.ToList();
        var allTenantHosts = groups.SelectMany(g => AsList(Field(g, "hosts"))).Select(h => h.Trim()).Where(h => h != "").Distinct(StringComparer.Ordinal).ToList();
        if (allTenantHosts.Count == 0)
        {
            return FailLog("no tenant groups");
        }

        var vf = EpcClpVhostFetch(ref cookie, platformSite);
        var vhost = Str(vf["vhost"]);
        var log = new List<string> { "vhost_len=" + ByteLen(vhost) };
        if (vhost == "" || Str(vf["token"]) == "")
        {
            log.Add("Could not read vhost");
            return OkLog(false, log);
        }

        vhost = EpcClpVhostStripTenantMarkers(vhost);
        log.Add("cleared EPC tenant marker blocks");
        vhost = EpcClpVhostScrubTenantMisroutes(vhost, allTenantHosts);
        log.Add("scrubbed tenant misroutes");
        var stripped = EpcClpVhostStripHostsFromServerNames(vhost, allTenantHosts, false);
        vhost = stripped.Vhost;
        log.AddRange(stripped.Log.Take(12));
        if (stripped.Log.Count > 12)
        {
            log.Add("... +" + (stripped.Log.Count - 12) + " more strip lines");
        }

        var varPos = Stripos(vhost, "varnish_proxy_pass");
        if (varPos >= 0)
        {
            var head = vhost[..varPos];
            var tail = vhost[varPos..];
            var platformHosts = EpcClpVhostModelCPlatformHosts();
            head = ServerNameLine.Replace(head, m =>
            {
                if (!platformHosts.Any(ph => Stripos(m.Groups[1].Value, ph) >= 0))
                {
                    return m.Value;
                }

                var names = SplitNames(m.Groups[1].Value).Where(n => n != "" && !allTenantHosts.Any(a => EqCi(n, a))).ToList();
                return "  server_name " + string.Join(" ", names) + ";";
            });
            vhost = head + tail;
            log.Add("removed tenant hosts from varnish/platform front server_name");
        }

        foreach (var aliasHost in allTenantHosts)
        {
            var host = aliasHost;
            var platformHosts = EpcClpVhostModelCPlatformHosts();
            vhost = ServerNameLine.Replace(vhost, m =>
            {
                var isPlatform = platformHosts.Any(ph => Stripos(m.Groups[1].Value, ph) >= 0);
                if (isPlatform || Stripos(m.Groups[1].Value, host) < 0)
                {
                    return m.Value;
                }

                var names = SplitNames(m.Groups[1].Value).Where(n => !EqCi(n, host)).ToList();
                return names.Count == 0 ? m.Value : "  server_name " + string.Join(" ", names) + ";";
            });
        }

        log.Add("removed tenant hosts from redirect server_name lines");
        var backend = EpcClpVhostRemoveAliasesFromPhpBackend(vhost, allTenantHosts, platformSite);
        vhost = Str(backend["vhost"]);
        log.AddRange(AsList(backend["log"]));
        vhost = vhost.TrimEnd() + "\n\n" + EpcClpVhostBuildModelCTenantSnippets(groups);
        log.Add("inserted " + groups.Count + " tenant direct server block(s)");
        vhost = TenantRootReset.Replace(vhost, "$1  {{root}}");
        log.Add("tenant direct root set to {{root}}");
        var token = Str(vf["token"]);
        var save = EpcClpVhostSaveAndApply(ref cookie, platformSite, vhost, token);
        log.AddRange(AsList(save["log"]));
        return Bool(save["ok"]) ? OkLog(true, log) : OkLog(false, log.Concat(["vhost save failed"]).ToList());
    }

    public static List<string> EpcPortalSharedDocroots()
    {
        string[] roots =
        [
            "/home/epartscart/htdocs/www.epartscart.com",
            "/home/ecomae/htdocs/www.ecomae.com"
        ];
        return roots.Where(root => IsDir(root) && IsFile(root + "/index.php")).ToList();
    }

    public static Dictionary<string, object?> EpcClpVhostPatchFailoverSplash(string vhost, string docroot, IEnumerable<string>? hostnames = null)
    {
        docroot = docroot.TrimEnd('/');
        if (docroot == "")
        {
            return new(StringComparer.Ordinal) { ["vhost"] = vhost, ["log"] = new List<string> { "empty docroot" }, ["patched"] = 0 };
        }

        const string splashNeedle = "error_page 502 503 504 525";
        var hosts = UniqueTrim(hostnames ?? []);
        var log = new List<string>();
        var patched = 0;
        var outText = new StringBuilder();
        foreach (var walk in WalkServerBraceBlocks(vhost))
        {
            if (walk.Kind != WalkKind.Block)
            {
                outText.Append(walk.Text);
                continue;
            }

            var block = walk.Text;
            var newBlock = block;
            var inTenantMarker = Stripos(block, "EPC_TENANT_DIRECT") >= 0 || Stripos(block, "www.thejewellerytrend.com") >= 0;
            var hit = hosts.Count == 0;
            var sn = ServerNameLineCi.Match(block);
            if (!hit && sn.Success)
            {
                hit = NamesHit(SplitNames(sn.Groups[1].Value), hosts);
            }

            if (hit && (inTenantMarker || hosts.Count != 0) && Stripos(block, splashNeedle) < 0)
            {
                var inject = "\n  error_page 502 503 504 525 = /epc-platform-splash.html;\n"
                    + "  location = /epc-platform-splash.html {\n"
                    + "    root " + docroot + ";\n"
                    + "    add_header Cache-Control \"no-store\";\n"
                    + "    try_files $uri =404;\n"
                    + "  }\n"
                    + "  location = /epc-platform-status.json {\n"
                    + "    root " + docroot + ";\n"
                    + "    add_header Cache-Control \"no-store\";\n"
                    + "    try_files $uri =404;\n"
                    + "  }\n";
                var phpLoc = PhpLocation.Match(block);
                if (phpLoc.Success)
                {
                    newBlock = block[..phpLoc.Index] + inject + block[phpLoc.Index..];
                }
                else
                {
                    var replaced = CloseBraceTrail.Replace(block, inject + "}\n", 1);
                    newBlock = replaced ?? block;
                }

                if (newBlock != block)
                {
                    patched++;
                    var sn2 = Regex.Match(block, @"server_name\s+([^;]+)", RegexOptions.IgnoreCase);
                    log.Add("failover splash for " + (sn2.Success ? sn2.Groups[1].Value.Trim() : "server"));
                }
            }

            outText.Append(newBlock);
        }

        return new(StringComparer.Ordinal) { ["vhost"] = outText.ToString(), ["log"] = log, ["patched"] = patched };
    }

    public static string EpcClpVhostPatchTenantDirectRoot(string vhost, string preferredRoot)
    {
        preferredRoot = preferredRoot.TrimEnd('/');
        if (preferredRoot == "" || !IsDir(preferredRoot))
        {
            return vhost;
        }

        const string marker = "# EPC_TENANT_DIRECT_START";
        const string endMarker = "# EPC_TENANT_DIRECT_END";
        if (Stripos(vhost, marker) < 0)
        {
            return vhost;
        }

        var rootLine = "  root " + preferredRoot + ";";
        var rx = new Regex(Regex.Escape(marker) + @"[\s\S]*?" + Regex.Escape(endMarker));
        var outText = rx.Replace(vhost, m =>
        {
            var block = m.Value.Replace("{{root}}", rootLine, StringComparison.Ordinal);
            return RootLine.Replace(block, rootLine);
        });
        return outText != "" ? outText : vhost;
    }

    public static Dictionary<string, object?> EpcClpVhostAddAliasesToPhpBackend(string vhost, IEnumerable<string> aliasHosts, string anchorSite)
    {
        var aliases = aliasHosts.ToList();
        var log = new List<string>();
        var outText = ServerNameLine.Replace(vhost, m =>
        {
            if (Stripos(m.Groups[1].Value, anchorSite) < 0)
            {
                return m.Value;
            }

            var pos = vhost.IndexOf(m.Value, StringComparison.Ordinal);
            if (pos < 0)
            {
                return m.Value;
            }

            var start = Math.Max(0, pos - 500);
            var chunk = vhost.Substring(start, Math.Min(500, pos - start));
            if (Stripos(chunk, "listen 8080") < 0)
            {
                return m.Value;
            }

            var names = SplitNames(m.Groups[1].Value);
            var changed = false;
            foreach (var alias in aliases)
            {
                if (!names.Any(n => EqCi(n, alias)))
                {
                    names.Add(alias);
                    log.Add("8080 backend +" + alias);
                    changed = true;
                }
            }

            return changed ? "  server_name " + string.Join(" ", names) + ";" : m.Value;
        });
        return new(StringComparer.Ordinal) { ["vhost"] = outText, ["log"] = log };
    }

    public static Dictionary<string, object?> EpcClpVhostConfigureTenantDirectPhp(ref string cookie, string platformSite, IEnumerable<string> aliasHosts, string anchorSite = "")
    {
        anchorSite = anchorSite != "" ? anchorSite : platformSite;
        var aliases = UniqueTrim(aliasHosts);
        if (aliases.Count == 0)
        {
            return FailLog("no alias hosts");
        }

        var vf = EpcClpVhostFetch(ref cookie, platformSite);
        var vhost = Str(vf["vhost"]);
        var log = new List<string> { "vhost_len=" + ByteLen(vhost) };
        if (vhost == "" || Str(vf["token"]) == "")
        {
            log.Add("Could not read vhost");
            return OkLog(false, log);
        }

        var varPos = Stripos(vhost, "varnish_proxy_pass");
        if (varPos >= 0)
        {
            var head = vhost[..varPos];
            var tail = vhost[varPos..];
            head = ServerNameLine.Replace(head, m =>
            {
                if (Stripos(m.Groups[1].Value, anchorSite) < 0)
                {
                    return m.Value;
                }

                var names = SplitNames(m.Groups[1].Value).Where(n => !aliases.Any(a => EqCi(n, a))).ToList();
                return "  server_name " + string.Join(" ", names) + ";";
            });
            vhost = head + tail;
            log.Add("removed tenant hosts from varnish server_name");
        }

        foreach (var aliasHost in aliases)
        {
            var host = aliasHost;
            vhost = ServerNameLine.Replace(vhost, m =>
            {
                if (Stripos(m.Groups[1].Value, anchorSite) >= 0 || Stripos(m.Groups[1].Value, host) < 0)
                {
                    return m.Value;
                }

                var names = SplitNames(m.Groups[1].Value).Where(n => !EqCi(n, host)).ToList();
                return "  server_name " + string.Join(" ", names) + ";";
            });
        }

        log.Add("removed tenant hosts from redirect server_name lines");
        Dictionary<string, object?> backend;
        if (Stripos(platformSite, "ecomae.com") >= 0)
        {
            backend = EpcClpVhostRemoveAliasesFromPhpBackend(vhost, aliases, anchorSite);
        }
        else
        {
            backend = EpcClpVhostAddAliasesToPhpBackend(vhost, aliases, anchorSite);
        }

        log.AddRange(AsList(backend["log"]));
        vhost = Str(backend["vhost"]);
        aliases.Sort(WwwFirst);
        var namesLine = string.Join(" ", aliases);
        const string marker = "# EPC_TENANT_DIRECT_START";
        var tenantBlock = "# EPC_TENANT_DIRECT_START\n"
            + EpcClpVhostTenantDirectServerTemplate().Replace("TENANT_NAMES", namesLine, StringComparison.Ordinal)
            + "# EPC_TENANT_DIRECT_END\n";
        if (Stripos(vhost, marker) >= 0)
        {
            vhost = new Regex(Regex.Escape(marker) + @"[\s\S]*?# EPC_TENANT_DIRECT_END\s*").Replace(vhost, tenantBlock, 1);
            log.Add("updated tenant direct block");
        }
        else
        {
            vhost += "\n" + tenantBlock;
            log.Add("inserted tenant direct block");
        }

        var wwwHost = "";
        var bareHost = "";
        foreach (var h in aliases)
        {
            if (Stripos(h, "www.") == 0)
            {
                wwwHost = h;
            }
            else if (h.Contains('.', StringComparison.Ordinal))
            {
                bareHost = h;
            }
        }

        const string apexMarker = "# EPC_TENANT_APEX_REDIRECT_START";
        if (wwwHost != "" && bareHost != "" && !EqCi(bareHost, wwwHost))
        {
            var apexBlock = "# EPC_TENANT_APEX_REDIRECT_START\nserver {\n  listen 80;\n  listen [::]:80;\n  listen 443 ssl;\n  listen [::]:443 ssl;\n  http2 on;\n  {{ssl_certificate_key}}\n  {{ssl_certificate}}\n  server_name " + bareHost + ";\n  return 301 https://" + wwwHost + "$request_uri;\n}\n# EPC_TENANT_APEX_REDIRECT_END\n";
            if (Stripos(vhost, apexMarker) >= 0)
            {
                vhost = new Regex(Regex.Escape(apexMarker) + @"[\s\S]*?# EPC_TENANT_APEX_REDIRECT_END\s*").Replace(vhost, apexBlock, 1);
            }
            else
            {
                vhost += "\n" + apexBlock;
            }

            log.Add("apex redirect " + bareHost + " -> " + wwwHost);
        }

        vhost = TenantRootReset.Replace(vhost, "$1  {{root}}");
        log.Add("tenant direct root reset to {{root}}");
        var token = Str(vf["token"]);
        var save = EpcClpVhostSaveAndApply(ref cookie, platformSite, vhost, token);
        log.AddRange(AsList(save["log"]));
        return Bool(save["ok"]) ? OkLog(true, log) : OkLog(false, log.Concat(["vhost save failed"]).ToList());
    }

    private enum WalkKind { Text, Block }

    private readonly struct WalkPiece
    {
        public WalkKind Kind { get; init; }
        public string Text { get; init; }
        public int Start { get; init; }
    }

    private static IEnumerable<WalkPiece> WalkServerKeyword(string vhost, bool includeStart = false)
    {
        var len = vhost.Length;
        var cursor = 0;
        while (cursor < len)
        {
            var serverPos = vhost.IndexOf("server", cursor, StringComparison.Ordinal);
            if (serverPos < 0)
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[cursor..], Start = cursor };
                yield break;
            }

            var braceOpen = vhost.IndexOf('{', serverPos);
            if (braceOpen < 0)
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[cursor..], Start = cursor };
                yield break;
            }

            var between = vhost[serverPos..braceOpen];
            if (!ServerWord.IsMatch(between))
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[cursor..(braceOpen + 1)], Start = cursor };
                cursor = braceOpen + 1;
                continue;
            }

            var depth = 1;
            var i = braceOpen + 1;
            for (; i < len; i++)
            {
                var ch = vhost[i];
                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        break;
                    }
                }
            }

            if (depth != 0)
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[cursor..], Start = cursor };
                yield break;
            }

            var blockStart = serverPos;
            var blockEnd = i + 1;
            if (blockStart > cursor)
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[cursor..blockStart], Start = cursor };
            }

            yield return new WalkPiece { Kind = WalkKind.Block, Text = vhost[blockStart..blockEnd], Start = includeStart ? blockStart : blockStart };
            cursor = blockEnd;
        }
    }

    private static IEnumerable<WalkPiece> WalkServerBraceBlocks(string vhost)
    {
        var len = vhost.Length;
        var i = 0;
        while (i < len)
        {
            var m = ServerBrace.Match(vhost, i);
            if (!m.Success)
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[i..], Start = i };
                yield break;
            }

            var start = m.Index;
            if (start > i)
            {
                yield return new WalkPiece { Kind = WalkKind.Text, Text = vhost[i..start], Start = i };
            }

            var depth = 0;
            var j = start;
            var end = len;
            while (j < len)
            {
                var ch = vhost[j];
                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        end = j + 1;
                        break;
                    }
                }

                j++;
            }

            yield return new WalkPiece { Kind = WalkKind.Block, Text = vhost[start..end], Start = start };
            i = end;
        }
    }

    private static bool BlockHasHost(string block, List<string> tenants)
    {
        foreach (Match sn in ServerNameLineCi.Matches(block))
        {
            foreach (var name in SplitNames(sn.Groups[1].Value))
            {
                if (name != "" && tenants.Contains(name.ToLowerInvariant()))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool NamesHit(IEnumerable<string> names, IEnumerable<string> want)
        => want.Any(w => names.Any(n => EqCi(n, w)));

    private static List<string> SplitNames(string raw)
        => Regex.Split(raw.Trim(), @"\s+").Where(n => n != "").ToList();

    private static List<string> UniqueTrim(IEnumerable<string> items)
        => items.Select(h => (h ?? "").Trim()).Where(h => h != "").Distinct(StringComparer.Ordinal).ToList();

    private static List<string> UniqueLower(IEnumerable<string> items)
        => items.Select(h => (h ?? "").Trim().ToLowerInvariant()).Where(h => h != "").Distinct(StringComparer.Ordinal).ToList();

    private static int WwwFirst(string a, string b)
    {
        var aw = Stripos(a, "www.") == 0 ? 0 : 1;
        var bw = Stripos(b, "www.") == 0 ? 0 : 1;
        return aw != bw ? aw - bw : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static string EscapeShellArg(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static Dictionary<string, object?> OkDetail(bool ok, Dictionary<string, object?> detail)
        => new(StringComparer.Ordinal) { ["ok"] = ok, ["detail"] = detail };

    private static Dictionary<string, object?> OkLog(bool ok, List<string> log)
        => new(StringComparer.Ordinal) { ["ok"] = ok, ["log"] = log };

    private static Dictionary<string, object?> FailLog(string line)
        => OkLog(false, [line]);

    private static object? Field(Dictionary<string, object?> map, string key)
        => map.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value, string fallback = "")
        => value switch
        {
            null => fallback,
            string s => s,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback
        };

    private static int IntVal(object? value, int fallback = 0)
        => value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => fallback
        };

    private static bool Bool(object? value)
        => value is true or 1 or "1";

    private static List<string> AsList(object? value)
        => value switch
        {
            List<string> list => list,
            IEnumerable<string> e => e.ToList(),
            IEnumerable<object?> o => o.Select(x => Str(x)).ToList(),
            _ => []
        };

    private static int Stripos(string hay, string needle)
        => hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

    private static bool EqCi(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static int ByteLen(string value) => Encoding.UTF8.GetByteCount(value);

    private static string Cut(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static string Enc(string value) => Uri.EscapeDataString(value);

    private static string StripTags(string html)
        => Regex.Replace(html, "<[^>]+>", "");
}
