using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1 named helpers. PHP identifiers kept for the inventory:
/// <c>epc_cp_request_route</c>, <c>epc_cp_has_admin_cookies</c>, <c>epc_cp_is_login_request</c>,
/// <c>epc_cp_bootstrap_light_active</c>, <c>epc_cp_bootstrap_light_init</c>,
/// <c>epc_deploy_token</c>, <c>epc_deploy_client_ip</c>, <c>epc_deploy_allowed_ips</c>,
/// <c>epc_deploy_lockdown_enabled</c>, <c>epc_deploy_forbidden</c>, <c>epc_deploy_require_token</c>,
/// <c>epc_redirect_safe_target</c>,
/// <c>epc_bos_public_actions</c>, <c>epc_bos_provider_only_actions</c>,
/// <c>epc_bos_ajax_action_name</c>, <c>epc_bos_ajax_entry_guard</c>, <c>epc_bos_csrf_meta</c>,
/// <c>epc_php_reference_surface</c>, <c>epc_php_reference_apply_deep_uri</c>,
/// <c>epc_php_reference_is_super_cp_host</c>, <c>epc_php_reference_try_route</c>,
/// <c>epc_cp_common_parity_targets</c>, <c>epc_cp_common_parity_packs</c>,
/// <c>epc_cp_common_parity_pack_applies</c>, <c>epc_cp_common_parity_host_map</c>,
/// <c>epc_prices_build_office_storage_data_info</c>,
/// <c>epc_perf_cache_dir</c>, <c>epc_perf_cache_key_safe</c>, <c>epc_perf_cache_get</c>,
/// <c>epc_perf_cache_set</c>, <c>epc_perf_cache_remember</c>, <c>epc_perf_cache_delete</c>,
/// <c>epc_perf_cache_bust_prefix</c>, <c>epc_cp_menu_cache</c>, <c>epc_cp_menu_cache_bust</c>,
/// <c>epc_portal_tenant_brand_catalog</c>, <c>epc_portal_tenant_brand_site_key</c>,
/// <c>epc_portal_tenant_brand_config</c>, <c>epc_portal_tenant_brand_enabled</c>,
/// <c>epc_portal_tenant_brand_css_href</c>, <c>epc_portal_tenant_brand_css_version</c>,
/// <c>epc_portal_tenant_brand_enqueue</c>, <c>epc_portal_tenant_brand_markup</c>,
/// <c>epc_portal_tenant_brand_hero_block</c>,
/// <c>epc_genuine_cache_path</c>, <c>epc_genuine_site_base_url</c>,
/// <c>epc_genuine_sync_umapi_sections</c>, <c>epc_genuine_count_umapi_rows</c>,
/// <c>epc_genuine_section_counts</c>, <c>epc_genuine_load_manufacturer_names</c>,
/// <c>epc_genuine_read_cache</c>, <c>epc_genuine_write_cache</c>,
/// <c>epc_genuine_build_frontend_index</c>.
/// </summary>
public static class PhpPlanQ1
{
    public const string BootstrapLightPath = "cp/epc_cp_bootstrap_light.php";
    public const string DeployAuthPath = "epc_deploy_auth.php";
    public const string BosSecurityPath = "content/general_pages/epc_bos_security.php";
    public const string PhpReferenceRouterPath = "content/general_pages/epc_php_reference_router.php";
    public const string CommonParityPath = "content/general_pages/epc_cp_common_parity.php";
    public const string OfficeStorageMetaPath = "content/shop/docpart/epc_prices_office_storage_meta.php";
    public const string PerfCachePath = "content/general_pages/epc_perf_cache.php";
    public const string TenantBrandPath = "content/general_pages/epc_portal_tenant_brand.php";
    public const string GenuineManufacturersPath = "content/shop/docpart/docpart_genuine_manufacturers.php";
    public const string DeployDefaultToken = "epartscart-deploy-2026";
    public const string TenantBrandCssHref = "/content/general_pages/epc_portal_tenant_brand.css";
    public const string TenantBrandCssVersion = "20260528c";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string EpcCpRequestRoute(string requestUri, string backendDir = "cp")
    {
        var q = requestUri.IndexOf('?', StringComparison.Ordinal);
        var path = q < 0 ? requestUri : requestUri[..q];
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        var backend = (backendDir ?? "").Trim('/');
        if (backend.Length == 0)
        {
            backend = "cp";
        }

        var basePath = "/" + backend;
        if (path == basePath || path == basePath + "/")
        {
            return "";
        }

        return path.StartsWith(basePath + "/", StringComparison.Ordinal)
            ? path[(basePath.Length + 1)..].TrimStart('/')
            : path.TrimStart('/');
    }

    public static bool EpcCpHasAdminCookies(string? adminSession, string? adminUserId)
    {
        var session = adminSession ?? "";
        var userId = StorefrontPhpInt.Cast(adminUserId);
        return session.Length > 0 && userId > 0;
    }

    public static bool EpcCpIsLoginRequest(string method, string route, bool hasAdminCookies, bool postAuthentication, bool demoLoginRoot = false, bool erpRequest = false, bool clientErpLoginRoot = false, bool clientErpActive = false, bool isAdmin = false)
    {
        if (demoLoginRoot)
        {
            return true;
        }

        if (erpRequest)
        {
            return false;
        }

        if (clientErpLoginRoot)
        {
            return true;
        }

        if (clientErpActive)
        {
            return !isAdmin || route.Length == 0 || route == "index.php";
        }

        if (string.Equals(method, "POST", StringComparison.Ordinal) && postAuthentication)
        {
            return true;
        }

        if (!string.Equals(method, "GET", StringComparison.Ordinal))
        {
            return false;
        }

        if (hasAdminCookies && isAdmin)
        {
            return false;
        }

        return route.Length == 0 || route == "control" || route == "index.php";
    }

    public static bool EpcCpBootstrapLightActive(bool flag) => flag;

    public static (bool Light, bool SessionCleared) EpcCpBootstrapLightInit(string method, string route, bool hasAdminCookies, bool isAdmin)
    {
        var sessionCleared = hasAdminCookies && !isAdmin;
        var cookies = hasAdminCookies && !sessionCleared;
        var login = EpcCpIsLoginRequest(method, route, cookies, false, isAdmin: isAdmin);
        return (login, sessionCleared);
    }

    public static string EpcDeployToken(string? envToken)
        => string.IsNullOrEmpty(envToken) ? DeployDefaultToken : envToken;

    public static string EpcDeployClientIp(IReadOnlyDictionary<string, string> server)
    {
        var remote = server.TryGetValue("REMOTE_ADDR", out var r) ? r : "";
        var trustProxy = remote.Length > 0 && (remote is "127.0.0.1" or "::1" || !IsPublicIp(remote));
        string[] keys = trustProxy
            ? ["HTTP_CF_CONNECTING_IP", "HTTP_X_FORWARDED_FOR", "REMOTE_ADDR"]
            : ["REMOTE_ADDR"];
        foreach (var key in keys)
        {
            if (!server.TryGetValue(key, out var raw) || string.IsNullOrEmpty(raw))
            {
                continue;
            }

            if (key == "HTTP_X_FORWARDED_FOR")
            {
                raw = raw.Split(',')[0].Trim();
            }

            if (IPAddress.TryParse(raw, out _))
            {
                return raw;
            }
        }

        return "0.0.0.0";
    }

    public static string[] EpcDeployAllowedIps(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }

    public static bool EpcDeployLockdownEnabled(string documentRoot)
        => File.Exists(documentRoot.TrimEnd('/') + "/.epc-security-lockdown");

    public static Dictionary<string, object?> EpcDeployForbidden(string message = "Forbidden")
        => new() { ["status"] = false, ["message"] = message };

    public static Dictionary<string, object?> EpcDeployRequireToken(string expected, string given, string[] allowedIps, string clientIp, bool lockdown, string risk)
    {
        if (given.Length == 0 || !CryptographicEquals(expected, given))
        {
            return EpcDeployForbidden();
        }

        if (allowedIps.Length > 0 && !allowedIps.Contains(clientIp, StringComparer.Ordinal))
        {
            return EpcDeployForbidden();
        }

        if (risk == "secret" && lockdown && allowedIps.Length == 0)
        {
            return EpcDeployForbidden();
        }

        if (lockdown && expected == DeployDefaultToken && risk == "secret")
        {
            return EpcDeployForbidden();
        }

        return new Dictionary<string, object?> { ["ok"] = true };
    }

    public static string EpcRedirectSafeTarget(string target)
    {
        target = target.Trim();
        if (target.Length == 0 || target == "/")
        {
            return "/";
        }

        if (Regex.IsMatch(target, "^https?://", RegexOptions.IgnoreCase) || target.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        if (target[0] != '/')
        {
            return "/";
        }

        return Regex.IsMatch(target, "[\r\n\x00]") ? "/" : target;
    }

    public static string[] EpcBosPublicActions() => ["login"];

    public static string[] EpcBosProviderOnlyActions() =>
    [
        "tenant_list", "tenant_info", "switch_tenant", "fleet_health", "tenant_compliance",
        "system_health", "isolation_audit", "mfa_policy", "mfa_stats", "webhooks", "events",
        "design_tokens", "readiness_score", "notifications", "db_migrations", "cp_role_home",
        "credit_limit", "order_erp_pipeline", "po_approval", "rest_api_v2", "fulfillment_queue",
        "bi_metrics", "ai_classification", "tenant_config", "workflow_builder", "inventory_forecast",
        "multi_currency_gl", "sso_saml", "wps_payroll", "collections_dunning", "warranty_rma",
        "dealer_portal", "ai_copilot", "nl_reporting", "industry_packs", "multi_entity",
        "promotions_engine", "config_sandbox", "import_orchestrator", "document_vault",
        "subscription_billing", "soc2_compliance", "marketplace", "ai_service", "metabase_embed",
        "isolation_anomaly"
    ];

    public static string EpcBosAjaxActionName(string? postAction, string? getAction, bool hasEmailPassword)
    {
        var action = (postAction ?? "").Trim();
        if (action.Length == 0)
        {
            action = (getAction ?? "").Trim();
        }

        return action.Length == 0 && hasEmailPassword ? "login" : action;
    }

    public static Dictionary<string, object?> EpcBosAjaxEntryGuard(string action, string role, int userId, string method, string subAction, bool csrfOk, bool sessionValid)
    {
        if (action == "login")
        {
            return new Dictionary<string, object?> { ["ok"] = true, ["rate_limit"] = "bos_login" };
        }

        if (action.Length == 0 || action == "Invalid action")
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Unknown action", ["http"] = 400 };
        }

        if (role == "guest" || userId <= 0)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Authentication required", ["http"] = 401 };
        }

        if (!sessionValid)
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Session expired", ["http"] = 401 };
        }

        method = method.ToUpperInvariant();
        var mutating = method is "POST" or "PUT" or "PATCH" or "DELETE";
        string[] readOnlySafe = ["tenant_list", "tenant_info", "fleet_health", "system_health", "notifications", "readiness_score", "mfa_stats", "bi_metrics"];
        if (mutating && !readOnlySafe.Contains(action, StringComparer.Ordinal))
        {
            string[] safeSub = ["list", "get", "stats", "run_audit", "latest_run", "recent_violations"];
            string[] csrfActions = ["switch_tenant", "tenant_config", "config_sandbox", "import_orchestrator", "document_vault", "mfa_policy", "webhooks", "design_tokens"];
            var needsCsrf = (subAction.Length > 0 && !safeSub.Contains(subAction, StringComparer.Ordinal))
                || csrfActions.Contains(action, StringComparer.Ordinal);
            if (needsCsrf && !csrfOk)
            {
                return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "CSRF validation failed", ["csrf_required"] = true, ["http"] = 403 };
            }
        }

        if (EpcBosProviderOnlyActions().Contains(action, StringComparer.Ordinal) && role != "provider")
        {
            return new Dictionary<string, object?> { ["ok"] = false, ["error"] = "Provider access required", ["http"] = 403 };
        }

        return new Dictionary<string, object?> { ["ok"] = true };
    }

    public static string EpcBosCsrfMeta(string token)
        => "<meta name=\"epc-bos-csrf\" content=\"" + H(token) + "\">"
            + "<script>window.EPC_BOS_CSRF=" + JsonSerializer.Serialize(token, JsonOpts) + ";</script>";

    public static string EpcPhpReferenceSurface(string? raw)
    {
        raw = (raw ?? "").Trim().ToLowerInvariant();
        return raw is "home" or "cp" or "erp" or "bos" or "storefront" ? raw : "";
    }

    public static string EpcPhpReferenceApplyDeepUri(string surface, string path, IReadOnlyDictionary<string, string> query)
    {
        path = "/" + path.TrimStart('/');
        if (surface == "cp")
        {
            if (!Regex.IsMatch(path, "^/(?:CP|cp)(?:/|\\?|$)"))
            {
                path = "/cp/";
            }
        }
        else if (surface == "erp")
        {
            if (!Regex.IsMatch(path, "^/(?:ERP|erp)(?:/|\\?|$)"))
            {
                path = "/erp";
            }
        }
        else if (surface == "bos")
        {
            path = "/bos/";
        }

        var qs = string.Join("&", query.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
        return path + (qs.Length > 0 ? "?" + qs : "");
    }

    public static bool EpcPhpReferenceIsSuperCpHost(string? host)
    {
        host = Regex.Replace((host ?? "").ToLowerInvariant(), ":\\d+$", "");
        host = Regex.Replace(host, "^www\\.", "");
        return host is "ecomae.com" or "cp.ecomae.com";
    }

    public static bool EpcPhpReferenceTryRoute(string? raw)
        => EpcPhpReferenceSurface(raw).Length > 0;

    public static Dictionary<string, Dictionary<string, object?>> EpcCpCommonParityTargets()
        => new()
        {
            ["ecomae"] = Target("www.ecomae.com", ["ecomae.com", "cp.ecomae.com"], "platform_control", "platform", "ecomae", "ecomae.com / CP — overall platform control"),
            ["epartscart"] = Target("www.epartscart.com", ["epartscart.com"], "spare_parts", "auto_parts", "docpart", "epartscart.com — spare parts only"),
            ["electronicae"] = Target("www.electronicae.com", [], "industry_storefront", "electronics", "docpart", "electronicae.com — electronics"),
            ["taxofinca"] = Target("www.taxofinca.com", [], "industry_storefront", "tax_advisory", "docpart", "taxofinca.com — tax advisory"),
            ["stylenlook"] = Target("www.stylenlook.com", [], "industry_storefront", "fashion", "docpart", "stylenlook.com — fashion"),
            ["thejewellerytrend"] = Target("www.thejewellerytrend.com", [], "industry_storefront", "jewellery", "docpart", "thejewellerytrend.com — jewellery")
        };

    public static Dictionary<string, Dictionary<string, object?>> EpcCpCommonParityPacks()
        => new()
        {
            ["oms_orders_menu"] = Pack("OMS · Orders — one Shop sidebar entry (no statuses/items rows)", "common", ["*"], ["platform_control", "spare_parts", "industry_storefront"]),
            ["cp_menu_full"] = Pack("Full CP menu parity (Customers & accounts, Documents, Payments, Marketing, Integrations, …)", "common", ["*"], ["platform_control", "spare_parts", "industry_storefront"]),
            ["oms_daily_guide"] = Pack("OMS daily guide route (/cp/shop/orders/oms-guide)", "common", ["*"], ["platform_control", "spare_parts", "industry_storefront"]),
            ["multivendor_upload"] = Pack("Multivendor price upload CP (ajaxUrl always configured)", "common", ["*"], ["platform_control", "spare_parts", "industry_storefront"], true),
            ["vehicle_catalog"] = Pack("Vehicle / VIN catalog CP", "industry", ["auto_parts"], ["spare_parts"]),
            ["platform_governance"] = Pack("Platform governance / tenant control", "platform", ["platform"], ["platform_control"])
        };

    public static bool EpcCpCommonParityPackApplies(string packKey, IReadOnlyDictionary<string, object?> target)
    {
        var packs = EpcCpCommonParityPacks();
        if (!packs.TryGetValue(packKey, out var pack))
        {
            return false;
        }

        var role = target.TryGetValue("role", out var roleObj) ? Convert.ToString(roleObj, CultureInfo.InvariantCulture) ?? "" : "";
        var industry = target.TryGetValue("industry", out var indObj) ? Convert.ToString(indObj, CultureInfo.InvariantCulture) ?? "" : "";
        var roles = (string[])pack["applies_to_roles"]!;
        if (roles.Length > 0 && !roles.Contains(role, StringComparer.Ordinal) && !roles.Contains("*", StringComparer.Ordinal))
        {
            return false;
        }

        var inds = (string[])pack["industries"]!;
        return inds.Contains("*", StringComparer.Ordinal) || inds.Contains(industry, StringComparer.Ordinal);
    }

    public static Dictionary<string, string> EpcCpCommonParityHostMap()
        => EpcCpCommonParityTargets().ToDictionary(kv => kv.Key, kv => (string)kv.Value["host"]!);

    public static object[] EpcPricesBuildOfficeStorageDataInfo(
        IReadOnlyList<Dictionary<string, object?>> bunches,
        IReadOnlyDictionary<int, StorageRow> storages,
        IReadOnlyDictionary<int, string> offices,
        IReadOnlyDictionary<string, int> additionalTime,
        IReadOnlyDictionary<string, List<Dictionary<string, object?>>> markups,
        IReadOnlyDictionary<string, string> managerCaptions,
        IReadOnlySet<int>? disabledStorages = null,
        IReadOnlySet<int>? disabledPrices = null)
    {
        var info = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        var priceIds = new List<int>();
        var pairs = new List<(int OfficeId, int StorageId)>();
        foreach (var bunch in bunches)
        {
            var officeId = Convert.ToInt32(bunch.TryGetValue("office_id", out var o) ? o : 0, CultureInfo.InvariantCulture);
            var storageId = Convert.ToInt32(bunch.TryGetValue("storage_id", out var s) ? s : 0, CultureInfo.InvariantCulture);
            if (storageId < 1)
            {
                continue;
            }

            pairs.Add((officeId, storageId));
        }

        if (pairs.Count == 0)
        {
            return [info, priceIds];
        }

        foreach (var (officeId, storageId) in pairs)
        {
            if (disabledStorages is not null && disabledStorages.Contains(storageId))
            {
                continue;
            }

            if (!storages.TryGetValue(storageId, out var row) || row.TempDisabled == 1)
            {
                continue;
            }

            if (row.PriceId < 1 || (disabledPrices is not null && disabledPrices.Contains(row.PriceId)))
            {
                continue;
            }

            var key = officeId + ":" + storageId;
            var caption = row.ShortName.Length > 0 ? row.ShortName : row.Name;
            if (managerCaptions.TryGetValue(key, out var mgr) && mgr.Length > 0)
            {
                caption = mgr;
            }

            var extra = additionalTime.TryGetValue(key, out var mins) ? mins / 24 : 0;
            var marks = markups.TryGetValue(key, out var list) ? list : [];
            var priceKey = row.PriceId.ToString(CultureInfo.InvariantCulture);
            if (!info.TryGetValue(priceKey, out var bucket))
            {
                bucket = [];
                info[priceKey] = bucket;
            }

            bucket.Add(new Dictionary<string, object?>
            {
                ["price_id"] = row.PriceId,
                ["storage_id"] = storageId,
                ["office_id"] = officeId,
                ["probability"] = row.Probability,
                ["color"] = row.Color,
                ["rate"] = row.Rate,
                ["additional_time"] = extra,
                ["office_caption"] = offices.TryGetValue(officeId, out var cap) ? cap : "",
                ["storage_caption"] = caption,
                ["markups"] = marks
            });
            priceIds.Add(row.PriceId);
        }

        return [info, priceIds.Distinct().ToList()];
    }

    public static string EpcPerfCacheDir(string documentRoot)
    {
        var dir = documentRoot.TrimEnd('/') + "/content/files/epc_cache";
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string EpcPerfCacheKeySafe(string key)
        => Regex.Replace(key, "[^a-zA-Z0-9_.\\-]", "_");

    public static object? EpcPerfCacheGet(string documentRoot, string key, long? now = null)
    {
        var file = EpcPerfCacheDir(documentRoot) + "/" + EpcPerfCacheKeySafe(key) + ".json";
        if (!File.Exists(file))
        {
            return null;
        }

        var raw = File.ReadAllText(file);
        if (raw.Length == 0)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(raw);
        if (!doc.RootElement.TryGetProperty("exp", out var exp) || !doc.RootElement.TryGetProperty("v", out var value))
        {
            return null;
        }

        if (exp.GetInt64() < (now ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
        {
            File.Delete(file);
            return null;
        }

        return JsonSerializer.Deserialize<object>(value.GetRawText());
    }

    public static void EpcPerfCacheSet(string documentRoot, string key, object? value, int ttl = 300, long? now = null)
    {
        if (ttl < 1)
        {
            return;
        }

        var file = EpcPerfCacheDir(documentRoot) + "/" + EpcPerfCacheKeySafe(key) + ".json";
        var payload = new Dictionary<string, object?>
        {
            ["exp"] = (now ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()) + ttl,
            ["v"] = value
        };
        File.WriteAllText(file, JsonSerializer.Serialize(payload, JsonOpts));
    }

    public static object? EpcPerfCacheRemember(string documentRoot, string key, int ttl, Func<object?> factory)
    {
        var cached = EpcPerfCacheGet(documentRoot, key);
        if (cached is not null)
        {
            return cached;
        }

        var value = factory();
        EpcPerfCacheSet(documentRoot, key, value, ttl);
        return value;
    }

    public static bool EpcPerfCacheDelete(string documentRoot, string key)
    {
        var file = EpcPerfCacheDir(documentRoot) + "/" + EpcPerfCacheKeySafe(key) + ".json";
        if (!File.Exists(file))
        {
            return false;
        }

        File.Delete(file);
        return true;
    }

    public static int EpcPerfCacheBustPrefix(string documentRoot, string prefix)
    {
        var dir = EpcPerfCacheDir(documentRoot);
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        var removed = 0;
        var safe = EpcPerfCacheKeySafe(prefix);
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var baseName = Path.GetFileNameWithoutExtension(file);
            if (safe.Length == 0 || baseName.StartsWith(safe, StringComparison.Ordinal))
            {
                File.Delete(file);
                removed++;
            }
        }

        return removed;
    }

    public static Dictionary<string, object?> EpcCpMenuCache(IReadOnlyList<Dictionary<string, object?>> groups, IReadOnlyList<Dictionary<string, object?>> items)
        => new() { ["groups"] = groups, ["items"] = items };

    public static int EpcCpMenuCacheBust(string documentRoot, string dbName)
        => EpcPerfCacheDelete(documentRoot, "epc_cp_menu_rows:v1:" + dbName) ? 1 : 0;

    public static Dictionary<string, Dictionary<string, string>> EpcPortalTenantBrandCatalog()
        => new(StringComparer.Ordinal)
        {
            ["electronicae"] = Brand("Electronicae", "electronicae.png", "TECH • GAMING • UAE", "#e10a0a", "rgba(225,10,10,.24)", "circuit"),
            ["stylenlook"] = Brand("Stylenlook", "stylenlook.png", "FASHION & BEAUTY", "#ec4899", "rgba(236,72,153,.26)", "fashion"),
            ["thejewellerytrend"] = Brand("The Jewellery Trend", "thejewellerytrend.png", "STYLE • SPARKLE • SHINE", "#d97706", "rgba(217,119,6,.32)", "sparkle"),
            ["taxofinca"] = Brand("TaxoFinca", "taxofinca.png", "TAX & ACCOUNTING SOLUTIONS", "#227a40", "rgba(34,122,64,.24)", "advisory")
        };

    public static string? EpcPortalTenantBrandSiteKey(string? siteKey, string? host)
    {
        var catalog = EpcPortalTenantBrandCatalog();
        var candidate = Regex.Replace((siteKey ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (candidate.Length > 0 && catalog.ContainsKey(candidate))
        {
            return candidate;
        }

        host = Regex.Replace((host ?? "").Trim().ToLowerInvariant(), "^www\\.", "");
        foreach (var key in catalog.Keys)
        {
            if (host == key + ".com" || host == key)
            {
                return key;
            }
        }

        return null;
    }

    public static Dictionary<string, string>? EpcPortalTenantBrandConfig(string? siteKey, string? host)
    {
        var key = EpcPortalTenantBrandSiteKey(siteKey, host);
        return key is not null ? EpcPortalTenantBrandCatalog()[key] : null;
    }

    public static bool EpcPortalTenantBrandEnabled(bool clientHost, string? siteKey, string? industry, string? host)
    {
        if (!clientHost)
        {
            return false;
        }

        var cleaned = Regex.Replace((siteKey ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (cleaned == "epartscart" || industry == "auto_parts")
        {
            return false;
        }

        return EpcPortalTenantBrandConfig(siteKey, host) is not null;
    }

    public static string EpcPortalTenantBrandCssHref() => TenantBrandCssHref;

    public static string EpcPortalTenantBrandCssVersion() => TenantBrandCssVersion;

    public static string EpcPortalTenantBrandEnqueue(bool enabled)
        => enabled
            ? "<link rel=\"stylesheet\" href=\"" + H(TenantBrandCssHref) + "?v=" + H(TenantBrandCssVersion) + "\" />\n"
            : "";

    public static string EpcPortalTenantBrandMarkup(string variant, string? siteKey, string? host)
    {
        var brand = EpcPortalTenantBrandConfig(siteKey, host);
        if (brand is null)
        {
            return "";
        }

        variant = variant is "header" or "hero" or "compact" ? variant : "header";
        var label = brand["label"];
        var tagline = brand["tagline"];
        var logo = brand["logo_url"];
        var accent = brand["accent"];
        var glow = brand["glow"];
        var animation = Regex.Replace(brand["animation"], "[^a-z0-9_-]", "");
        var aria = label + (tagline.Length > 0 ? " — " + tagline : "");
        var loading = variant == "hero" ? "eager" : "lazy";
        var html = new StringBuilder();
        html.Append("<span class=\"epc-tenant-brand epc-tenant-brand--").Append(H(variant))
            .Append(" epc-tenant-brand--").Append(H(animation)).Append("\" role=\"img\" aria-label=\"")
            .Append(H(aria)).Append("\" style=\"--etb-accent:").Append(H(accent)).Append(";--etb-glow:")
            .Append(H(glow)).Append("\">\n\t<span class=\"epc-tenant-brand__halo\" aria-hidden=\"true\"></span>\n\t<span class=\"epc-tenant-brand__logo-wrap\">\n\t\t<img class=\"epc-tenant-brand__logo\" src=\"")
            .Append(H(logo)).Append("\" alt=\"\" width=\"320\" height=\"96\" loading=\"").Append(loading).Append("\" />\n\t</span>");
        if (variant != "compact" && tagline.Length > 0)
        {
            html.Append("\n\t\t<span class=\"epc-tenant-brand__tagline\">").Append(H(tagline)).Append("</span>");
        }

        return html.Append("\n\t</span>\n\t").ToString();
    }

    public static string EpcPortalTenantBrandHeroBlock(bool enabled, string? siteKey, string? host)
    {
        if (!enabled)
        {
            return "";
        }

        return "<section class=\"epc-tenant-brand-hero col-lg-12\" aria-label=\"Brand presentation\">\n\t<div class=\"epc-tenant-brand-hero__inner\">\n\t\t"
            + EpcPortalTenantBrandMarkup("hero", siteKey, host)
            + "</div>\n</section>\n\t";
    }

    public static string EpcGenuineCachePath(string dir)
        => dir.TrimEnd('/', '\\') + "/cache/epc_genuine_manufacturers.json";

    public static string EpcGenuineSiteBaseUrl(string? domainPath, bool https, string host)
    {
        if (string.IsNullOrEmpty(domainPath))
        {
            return (https ? "https" : "http") + "://" + host;
        }

        return domainPath.TrimEnd('/');
    }

    public static string[] EpcGenuineSyncUmapiSections(string baseUrl)
        => new[] { "passenger", "commercial", "motorbike" }
            .Select(s => baseUrl.TrimEnd('/') + "/api/umapi_proxy.php?action=manufacturers&section=" + Uri.EscapeDataString(s) + "&language=en&region=WWW")
            .ToArray();

    public static int EpcGenuineCountUmapiRows(IReadOnlyList<(string Manufacturer, string Section)> rows)
        => rows.Where(r => r.Section is "passenger" or "commercial" or "motorbike")
            .Select(r => r.Manufacturer)
            .Distinct(StringComparer.Ordinal)
            .Count();

    public static Dictionary<string, int> EpcGenuineSectionCounts(IReadOnlyList<(string Manufacturer, string Section)> rows)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["passenger"] = 0,
            ["commercial"] = 0,
            ["motorbike"] = 0
        };
        foreach (var group in rows.Where(r => counts.ContainsKey(r.Section)).GroupBy(r => r.Section, StringComparer.Ordinal))
        {
            counts[group.Key] = group.Select(r => r.Manufacturer).Distinct(StringComparer.Ordinal).Count();
        }

        return counts;
    }

    public static string[] EpcGenuineLoadManufacturerNames(IReadOnlyList<(string Manufacturer, string Section)> rows)
        => rows.Where(r => r.Section is "passenger" or "commercial" or "motorbike")
            .Select(r => r.Manufacturer.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    public static Dictionary<string, object?>? EpcGenuineReadCache(string dir)
    {
        var path = EpcGenuineCachePath(dir);
        if (!File.Exists(path))
        {
            return null;
        }

        var raw = File.ReadAllText(path);
        if (raw.Length == 0)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(raw);
    }

    public static void EpcGenuineWriteCache(string dir, object payload)
    {
        var path = EpcGenuineCachePath(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        }));
    }

    public static Dictionary<string, object?> EpcGenuineBuildFrontendIndex(
        IReadOnlyList<(string Manufacturer, string Section)> rows,
        IReadOnlyDictionary<string, string[]> synonymMap,
        string catalogUrl)
    {
        var names = EpcGenuineLoadManufacturerNames(rows);
        var brands = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            foreach (var equiv in PhpReadyNamed.DocpartSynonymNamesForBrand(name, synonymMap))
            {
                var key = PhpReadyNamed.DocpartSynonymNormalizeBrand(equiv);
                if (key.Length > 0)
                {
                    brands[key] = true;
                }
            }
        }

        return new Dictionary<string, object?>
        {
            ["brands"] = brands,
            ["meta"] = new Dictionary<string, object?>
            {
                ["source"] = "epart_catalog",
                ["sections"] = EpcGenuineSectionCounts(rows),
                ["total"] = names.Length,
                ["keys"] = brands.Count,
                ["catalog_url"] = catalogUrl
            }
        };
    }

    public readonly record struct StorageRow(int PriceId, int Probability, string Color, string Rate, string Name, string ShortName, int TempDisabled);

    private static Dictionary<string, object?> Target(string host, string[] alt, string role, string industry, string db, string label)
        => new()
        {
            ["host"] = host,
            ["alt_hosts"] = alt,
            ["role"] = role,
            ["industry"] = industry,
            ["db_hint"] = db,
            ["label"] = label
        };

    private static Dictionary<string, object?> Pack(string label, string scope, string[] industries, string[] roles, bool filesOnly = false)
    {
        var row = new Dictionary<string, object?>
        {
            ["label"] = label,
            ["scope"] = scope,
            ["industries"] = industries,
            ["applies_to_roles"] = roles
        };
        if (filesOnly)
        {
            row["files_only"] = true;
        }

        return row;
    }

    private static Dictionary<string, string> Brand(string label, string file, string tagline, string accent, string glow, string animation)
        => new()
        {
            ["label"] = label,
            ["logo_url"] = "/content/files/images/ecomae-platform/assets/" + file,
            ["tagline"] = tagline,
            ["accent"] = accent,
            ["glow"] = glow,
            ["animation"] = animation
        };

    private static bool IsPublicIp(string raw)
    {
        if (!IPAddress.TryParse(raw, out var ip))
        {
            return false;
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 10 || b[0] == 127 || b[0] >= 224)
            {
                return false;
            }

            if (b[0] == 192 && b[1] == 168)
            {
                return false;
            }

            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            {
                return false;
            }

            if (b[0] == 169 && b[1] == 254)
            {
                return false;
            }
        }

        return !IPAddress.IsLoopback(ip);
    }

    private static bool CryptographicEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left);
        var b = Encoding.UTF8.GetBytes(right);
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }

    private static string H(string? value) => StorefrontTinyPages.HtmlSpecialChars(value);
}
