using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Small Control Panel / storefront PHP scripts closed in this slice, verified against PHP 8.3 by
/// <c>Fixtures/CpSmallPages/golden.json</c>:
/// <list type="bullet">
/// <item><c>cp/content/control/epc_cp_page_guard.php</c> (<c>epc_cp_page_login_url</c>, <c>epc_cp_page_require_admin</c>, <c>epc_cp_page_include</c>)</item>
/// <item><c>cp/epc_cp_fast_tenant.php</c></item>
/// <item><c>cp/epc_portal_industry_switch.php</c></item>
/// <item><c>cp/content/control/portal/epc_platform_health_checkup_config.php</c></item>
/// <item><c>cp/content/control/portal/epc_platform_governance_config.php</c></item>
/// <item><c>cp/content/control/portal/epc_web_tracker_config.php</c></item>
/// <item><c>cp/content/shop/document_control/epc_document_control_config.php</c></item>
/// <item><c>cp/content/shop/logistics/api_debug.php</c></item>
/// <item><c>cp/content/shop/returns/returns.php</c> and <c>router.php</c></item>
/// <item><c>content/eparts_cata.php</c> and <c>content/eparts_product.php</c></item>
/// <item><c>content/shop/price_engine/epc_tax_advisory_taxonomy.php</c> (<c>epc_tax_advisory_seed_tree</c>)</item>
/// <item><c>content/shop/marketing/epc_marketing_schema.php</c> (<c>epc_marketing_ensure_schema</c>)</item>
/// </list>
/// </summary>
public static class PhpCpSmallScripts
{
    public const string PageGuardPath = "cp/content/control/epc_cp_page_guard.php";
    public const string FastTenantPath = "cp/epc_cp_fast_tenant.php";
    // PHP names: epc_cp_page_login_url, epc_cp_page_require_admin, epc_cp_page_include,
    // epc_cp_is_fast_tenant_host, epc_cp_should_skip_erp_routers, epc_cp_fast_tenant_init, epc_cp_fast_tenant_active,
    // epc_tax_advisory_seed_tree, epc_marketing_ensure_schema.
    public const string IndustrySwitchPath = "cp/epc_portal_industry_switch.php";
    public const string HealthConfigPath = "cp/content/control/portal/epc_platform_health_checkup_config.php";
    public const string GovernanceConfigPath = "cp/content/control/portal/epc_platform_governance_config.php";
    public const string WebTrackerConfigPath = "cp/content/control/portal/epc_web_tracker_config.php";
    public const string DocumentControlConfigPath = "cp/content/shop/document_control/epc_document_control_config.php";
    public const string ApiDebugPath = "cp/content/shop/logistics/api_debug.php";
    public const string ReturnsPath = "cp/content/shop/returns/returns.php";
    public const string ReturnsRouterPath = "cp/content/shop/returns/router.php";
    public const string EpartsCataPath = "content/eparts_cata.php";
    public const string EpartsProductPath = "content/eparts_product.php";
    public const string TaxTaxonomyPath = "content/shop/price_engine/epc_tax_advisory_taxonomy.php";
    public const string MarketingSchemaPath = "content/shop/marketing/epc_marketing_schema.php";

    public const string IndustrySwitchUrl = "/cp/epc_portal_industry_switch.php";
    public const string HealthConfigUrl = "/cp/content/control/portal/epc_platform_health_checkup_config.php";
    public const string GovernanceConfigUrl = "/cp/content/control/portal/epc_platform_governance_config.php";
    public const string WebTrackerConfigUrl = "/cp/content/control/portal/epc_web_tracker_config.php";
    public const string DocumentControlConfigUrl = "/cp/content/shop/document_control/epc_document_control_config.php";
    public const string IndustrySessionKey = "epc_cp_industry_filter";
    public const string DefaultDeployToken = "epartscart-deploy-2026";

    private static readonly HashSet<string> ScriptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        IndustrySwitchUrl, HealthConfigUrl, GovernanceConfigUrl, WebTrackerConfigUrl, DocumentControlConfigUrl
    };

    private static readonly Regex IndustryCode = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsScriptPath(string? path) => path is not null && ScriptPaths.Contains(path);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(IndustrySwitchUrl, ["GET", "HEAD"], ServeIndustrySwitch)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(HealthConfigUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAdminJsAsync(context, connections, 403, admin => HealthConfig(admin)))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(GovernanceConfigUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAdminJsAsync(context, connections, 403, admin => GovernanceConfig(admin)))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(WebTrackerConfigUrl, ["GET", "HEAD", "POST"], ServeWebTrackerConfig)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(DocumentControlConfigUrl, ["GET", "HEAD", "POST"], ServeDocumentControlConfig)
            .DisableAntiforgery().AllowAnonymous();
    }

    public static string PageLoginUrl(string? backendDir)
    {
        var backend = backendDir is null ? "cp" : backendDir.Trim('/');
        return "/" + StorefrontTinyPages.HtmlSpecialChars(backend) + "/";
    }

    public static string? PageRequireAdmin(bool isAdmin, string featureLabel, string? backendDir)
    {
        if (isAdmin)
        {
            return null;
        }

        return "<div class=\"alert alert-warning\">Please <a href=\"" + PageLoginUrl(backendDir)
            + "\">log in to the control panel</a> to open "
            + StorefrontTinyPages.HtmlSpecialChars(featureLabel) + ".</div>";
    }

    public static string PageInclude(bool filePresent, string includeBody, string missingMessage)
        => filePresent
            ? includeBody
            : "<div class=\"alert alert-danger\">" + StorefrontTinyPages.HtmlSpecialChars(missingMessage) + "</div>";

    public static bool IsFastTenantHost(string? host)
    {
        host = (host ?? "").ToLowerInvariant();
        var colon = host.IndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }

        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host == "epartscart.com" || host.StartsWith("epartscart.", StringComparison.Ordinal);
    }

    public static bool ShouldSkipErpRouters(string? host, bool demoContext, string route)
    {
        if (!IsFastTenantHost(host) || demoContext)
        {
            return false;
        }

        return route.Length == 0
            || !(route.StartsWith("client-erp/", StringComparison.Ordinal) || route.StartsWith("platform-erp/", StringComparison.Ordinal));
    }

    public static string SanitizeIndustry(string? raw)
        => IndustryCode.Replace((raw ?? "").ToLowerInvariant(), "");

    public static (string Location, string? Industry) IndustrySwitch(string? industry, string? back, string? backendDir, bool known)
    {
        var backend = "/" + (backendDir ?? "cp");
        var location = back ?? backend;
        if (location.Contains("://", StringComparison.Ordinal) || location.Contains('\n'))
        {
            location = backend;
        }

        return (location, known && SanitizeIndustry(industry).Length > 0 ? SanitizeIndustry(industry) : null);
    }

    public static bool IndustryKnown(string? industry)
    {
        var code = SanitizeIndustry(industry);
        return code.Length > 0 && PhpPortalIndustryCatalog.Knows(code);
    }

    public static string HealthConfig(bool isAdmin, string token = DefaultDeployToken)
    {
        if (!isAdmin)
        {
            return "window.EPC_PHC={};";
        }

        var api = "https://www.ecomae.com/epc-platform-health-checkup-api.php?token=" + Uri.EscapeDataString(token);
        return "window.EPC_PHC = " + HexObject(("apiUrl", api), ("storageKey", "epc_platform_health_checkup_last_run")) + ";";
    }

    public static string GovernanceConfig(bool isAdmin, string? backendDir = "cp", string token = DefaultDeployToken)
    {
        if (!isAdmin)
        {
            return "window.EPC_PG={};";
        }

        var backend = (backendDir ?? "cp").Trim('/');
        var ajax = "/" + backend + "/content/control/portal/ajax_platform_governance.php";
        var health = "https://www.ecomae.com/epc-platform-governance-health-api.php?token=" + Uri.EscapeDataString(token);
        return "window.EPC_PG = " + HexObject(("ajaxUrl", ajax), ("healthApi", health)) + ";";
    }

    public static string WebTrackerConfig(string? backendDir, bool isSuper)
    {
        var backend = (backendDir ?? "cp").Trim('/');
        if (backend.Length == 0)
        {
            backend = "cp";
        }

        return "window.EPC_WEB_TRACKER_CP=" + SlashObject(
            ("ajaxUrl", "/" + backend + "/content/control/portal/ajax_epc_web_tracker.php"),
            ("isSuper", isSuper ? "true" : "false")) + ";\n";
    }

    public static string DocumentControlConfig(string? csrf, string? backendDir)
    {
        if (csrf is null)
        {
            return "window.EPC_DOCUMENT_CONTROL={};";
        }

        var backend = (backendDir ?? "cp").Trim('/');
        if (backend.Length == 0)
        {
            backend = "cp";
        }

        return "window.EPC_DOCUMENT_CONTROL=" + SlashObject(
            ("ajaxUrl", "/" + backend + "/content/shop/document_control/ajax_document_control_endpoint.php"),
            ("csrf", csrf)) + ";";
    }

    public static string ApiDebug(long storageId, bool debugOn, bool logPresent, string logBody, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        if (storageId > 0 && debugOn)
        {
            return logPresent ? logBody : T(3348);
        }

        return T(3349);
    }

    public static string ReturnsHub(string? backendDir, string? templateName, bool routerPresent, string routerBody)
    {
        var backend = backendDir ?? "";
        var template = templateName ?? "";
        // PHP 8 eats the newline after each `?>`; this file is CRLF.
        var html = "<div class=\"col-lg-12\">\r\n    <div class=\"hpanel\">\r\n        <div class=\"panel-heading hbuilt\">\r\n            {2113}        </div>\r\n        <div class=\"panel-body\">\r\n            <a class=\"panel_a\" href=\"/"
            + backend + "/shop/returns-manager\">\r\n                <div class=\"panel_a_img\" style=\"background: url('/"
            + backend + "/templates/" + template + "/images/special_search.png') 0 0 no-repeat;\"></div>\r\n                <div class=\"panel_a_caption\">{3815}</div>\r\n            </a>\r\n            <a class=\"panel_a\" href=\"/"
            + backend + "/shop/returns-manager?page=reasons_statuses&action=select\">\r\n                <div class=\"panel_a_img\" style=\"background: url('/"
            + backend + "/templates/" + template + "/images/warning.png') 0 0 no-repeat;\"></div>\r\n                <div class=\"panel_a_caption\">{3799}</div>\r\n            </a>\r\n            <a class=\"panel_a\" href=\"/"
            + backend + "\">\r\n                <div class=\"panel_a_img\" style=\"background: url('/"
            + backend + "/templates/" + template + "/images/power_off.png') 0 0 no-repeat;\"></div>\r\n                <div class=\"panel_a_caption\">{2116}</div>\r\n            </a>\r\n        </div>\r\n    </div>\r\n</div>\r\n";
        return html + (routerPresent
            ? routerBody
            : "<div class=\"col-lg-12\"><div class=\"alert alert-warning\">Returns router module is not installed on this host.</div></div>");
    }

    public static string ReturnsRouter(string? page, string? action, string? returnId, bool reasonsPresent, bool detailPresent, bool listPresent, string includeBody, string? backendDir)
    {
        var resolved = Regex.Replace((page ?? "list").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (resolved.Length == 0)
        {
            resolved = "list";
        }

        _ = Regex.Replace((action ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (!IsPhpEmpty(returnId) && (resolved == "list" || resolved.Length == 0))
        {
            resolved = "detail";
        }

        if (resolved == "reasons_statuses" && reasonsPresent)
        {
            return includeBody;
        }

        if (resolved == "detail" && detailPresent)
        {
            return includeBody;
        }

        if (listPresent)
        {
            return includeBody;
        }

        var backend = StorefrontTinyPages.HtmlSpecialChars(backendDir ?? "cp");
        return "<div class=\"col-lg-12\">\n\t<div class=\"hpanel\">\n\t\t<div class=\"panel-heading hbuilt\">Returns</div>\n\t\t<div class=\"panel-body\">\n\t\t\t<p class=\"text-muted\" style=\"margin:0\">Returns module files are missing on this host.</p>\n\t\t\t<p style=\"margin:12px 0 0\"><a href=\"/"
            + backend + "/shop/orders/orders\">Orders</a></p>\n\t\t</div>\n\t</div>\n</div>\n";
    }

    public static string EpartsProduct(string langHref, string? article, string? oem, string? brand, string? manufacturer, string? category)
    {
        langHref = string.IsNullOrEmpty(langHref) ? "/en" : langHref.TrimEnd('/');
        article = (article ?? oem ?? "").Trim();
        brand = (brand ?? manufacturer ?? "").Trim();
        category = (category ?? "").Trim();
        if (article.Length > 0)
        {
            var q = new Dictionary<string, string?> { ["article"] = article };
            if (brand.Length > 0)
            {
                q["brend"] = brand;
            }

            var target = langHref + "/shop/part_search?" + Rfc3986Query(q);
            var json = PhpCpTinyPages.PhpDefaultJson(target);
            var safe = StorefrontTinyPages.HtmlSpecialChars(target);
            return "<script>location = " + json + ";</script>"
                + "<noscript><meta http-equiv=\"refresh\" content=\"0;url=" + safe + "\"></noscript>"
                + "<p><a href=\"" + safe + "\">Continue to part search</a></p>";
        }

        var cata = langHref + "/eparts-cata";
        if (category.Length > 0)
        {
            cata += "?category=" + Uri.EscapeDataString(category);
        }

        var mod = langHref + "/eparts-mod";
        var search = langHref + "/shop/part_search";
        return "<div class=\"container\" style=\"padding:28px 16px;max-width:720px;\">\n"
            + "\t<h1 style=\"margin:0 0 8px;font-size:28px;\">EParts product</h1>\n"
            + "\t<p style=\"color:#64748b;margin:0 0 18px;\">Open the vehicle catalog to find OE / aftermarket parts, or browse by category.</p>\n"
            + "\t<p style=\"display:flex;gap:10px;flex-wrap:wrap;\">\n"
            + "\t\t<a class=\"btn btn-primary\" href=\"" + StorefrontTinyPages.HtmlSpecialChars(cata) + "\">EParts CATA</a>\n"
            + "\t\t<a class=\"btn btn-default\" href=\"" + StorefrontTinyPages.HtmlSpecialChars(mod) + "\">Select a vehicle</a>\n"
            + "\t\t<a class=\"btn btn-default\" href=\"" + StorefrontTinyPages.HtmlSpecialChars(search) + "\">Part search</a>\n"
            + "\t</p>\n</div>\n\t";
    }

    public static object TaxAdvisorySeedTree()
        => new object[]
        {
            Branch("svc-accounting", "Accounting & bookkeeping", 10, new object[]
            {
                Branch("svc-accounting-monthly", "Monthly bookkeeping packages", null, new object[]
                {
                    Leaf("svc-accounting-monthly-basic", "Basic SME package"),
                    Leaf("svc-accounting-monthly-growth", "Growth / multi-entity package"),
                    Leaf("svc-accounting-monthly-enterprise", "Enterprise / group reporting")
                }),
                Leaf("svc-accounting-payroll", "Payroll & WPS compliance"),
                Leaf("svc-accounting-management", "Management accounts & MIS")
            }),
            Branch("svc-audit", "Audit & assurance", 20, new object[]
            {
                Leaf("svc-audit-statutory", "Statutory audit (UAE mainland / free zone)"),
                Leaf("svc-audit-internal", "Internal audit & controls review"),
                Leaf("svc-audit-due-diligence", "Due diligence & transaction support")
            }),
            Branch("svc-tax", "Tax advisory & filing", 30, new object[]
            {
                Branch("svc-tax-vat", "VAT registration & filing", null, new object[]
                {
                    Leaf("svc-tax-vat-registration", "VAT registration (FTA)"),
                    Leaf("svc-tax-vat-return", "VAT return preparation & filing"),
                    Leaf("svc-tax-vat-refund", "VAT refund & voluntary disclosure")
                }),
                Branch("svc-tax-corporate", "Corporate tax (UAE CT)", null, new object[]
                {
                    Leaf("svc-tax-ct-registration", "Corporate tax registration"),
                    Leaf("svc-tax-ct-return", "Corporate tax return & transfer pricing"),
                    Leaf("svc-tax-ct-planning", "Tax planning & restructuring")
                }),
                Leaf("svc-tax-withholding", "Withholding tax & treaty advice")
            }),
            Branch("svc-advisory", "Business advisory", 40, new object[]
            {
                Leaf("svc-advisory-company-setup", "Company formation & licensing"),
                Leaf("svc-advisory-cfo", "Virtual CFO & financial advisory"),
                Leaf("svc-advisory-esr", "ESR / UBO / AML compliance support"),
                Leaf("svc-advisory-immigration", "PRO & immigration (corporate)")
            }),
            Branch("svc-consulting", "Consultancy packages", 50, new object[]
            {
                Leaf("svc-consulting-retainer", "Retainer advisory hours"),
                Leaf("svc-consulting-project", "Fixed-fee project engagements"),
                Leaf("svc-consulting-training", "Tax & finance training workshops")
            })
        };

    public static readonly string MarketingTaskProgressSql =
        "CREATE TABLE IF NOT EXISTS `epc_marketing_task_progress` (\n"
        + "			`strategy_key` VARCHAR(64) NOT NULL,\n"
        + "			`task_key` VARCHAR(128) NOT NULL,\n"
        + "			`is_done` TINYINT(1) NOT NULL DEFAULT 0,\n"
        + "			`done_at` INT UNSIGNED NULL DEFAULT NULL,\n"
        + "			`note` TEXT NULL,\n"
        + "			`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,\n"
        + "			PRIMARY KEY (`strategy_key`, `task_key`)\n"
        + "		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    public static readonly string MarketingKpiLogSql =
        "CREATE TABLE IF NOT EXISTS `epc_marketing_kpi_log` (\n"
        + "			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,\n"
        + "			`strategy_key` VARCHAR(64) NOT NULL DEFAULT '',\n"
        + "			`kpi_key` VARCHAR(128) NOT NULL,\n"
        + "			`value_decimal` DECIMAL(20,4) NULL DEFAULT NULL,\n"
        + "			`value_text` VARCHAR(512) NOT NULL DEFAULT '',\n"
        + "			`note` TEXT NULL,\n"
        + "			`recorded_at` INT UNSIGNED NOT NULL,\n"
        + "			`recorded_by` INT UNSIGNED NOT NULL DEFAULT 0,\n"
        + "			PRIMARY KEY (`id`),\n"
        + "			KEY `kpi_key` (`kpi_key`),\n"
        + "			KEY `recorded_at` (`recorded_at`)\n"
        + "		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    public static readonly string MarketingReviewsSql =
        "CREATE TABLE IF NOT EXISTS `epc_marketing_reviews` (\n"
        + "			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,\n"
        + "			`strategy_key` VARCHAR(64) NOT NULL,\n"
        + "			`review_type` VARCHAR(32) NOT NULL DEFAULT 'weekly',\n"
        + "			`score` TINYINT UNSIGNED NOT NULL DEFAULT 0,\n"
        + "			`notes` TEXT NULL,\n"
        + "			`created_at` INT UNSIGNED NOT NULL,\n"
        + "			`created_by` INT UNSIGNED NOT NULL DEFAULT 0,\n"
        + "			PRIMARY KEY (`id`),\n"
        + "			KEY `strategy_key` (`strategy_key`),\n"
        + "			KEY `created_at` (`created_at`)\n"
        + "		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private static Task ServeIndustrySwitch(HttpContext context)
    {
        var industry = StorefrontPhpQuery.Get(context.Request, "industry");
        var code = SanitizeIndustry(industry.Present && !industry.IsArray ? industry.Scalar : "");
        var known = code.Length > 0 && PhpPortalIndustryCatalog.Knows(code);
        var back = StorefrontPhpQuery.Get(context.Request, "back");
        var backValue = back.Present && !back.IsArray ? back.Scalar : null;
        var resolved = IndustrySwitch(code, backValue, "cp", known);
        if (resolved.Industry is not null)
        {
            context.Response.Cookies.Append(IndustrySessionKey, resolved.Industry, new CookieOptions { Path = "/" });
        }

        context.Response.Headers.Location = resolved.Location;
        context.Response.StatusCode = 302;
        return Task.CompletedTask;
    }

    private static async Task ServeAdminJsAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        int? forbiddenStatus,
        Func<bool, string> body)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        var admin = false;
        if (connections.IsConfigured)
        {
            try
            {
                var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                admin = await PhpCpConfigScripts.AdminCsrfAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    context.RequestAborted).ConfigureAwait(false) is not null;
            }
            catch (System.Data.Common.DbException)
            {
                admin = false;
            }
        }

        if (!admin && forbiddenStatus is int status)
        {
            response.StatusCode = status;
        }

        await response.WriteAsync(body(admin), context.RequestAborted).ConfigureAwait(false);
    }

    private static Task ServeWebTrackerConfig(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store";
        var host = context.Request.Host.Host;
        return response.WriteAsync(WebTrackerConfig("cp", PlatformHostPolicy.IsSuperCpHost(host)), context.RequestAborted);
    }

    private static async Task ServeDocumentControlConfig(HttpContext context, ITenantDbConnectionFactory connections)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        string? csrf = null;
        if (connections.IsConfigured)
        {
            try
            {
                var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                csrf = await PhpCpConfigScripts.AdminCsrfAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    context.RequestAborted).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
                csrf = null;
            }
        }

        await response.WriteAsync(DocumentControlConfig(csrf, "cp"), context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsPhpEmpty(string? value)
        => value is null || value.Length == 0 || value == "0" || value == "0.0";

    private static string Rfc3986Query(IReadOnlyDictionary<string, string?> parameters)
        => string.Join("&", parameters.Where(p => p.Value is not null).Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value!)));

    private static Dictionary<string, object> Leaf(string slug, string name)
        => new() { ["slug"] = slug, ["name"] = name };

    private static Dictionary<string, object> Branch(string slug, string name, int? sort, object[] children)
    {
        var row = new Dictionary<string, object> { ["slug"] = slug, ["name"] = name, ["children"] = children };
        if (sort is int value)
        {
            row["sort"] = value;
        }

        return row;
    }

    private static string HexObject(params (string Key, string Value)[] pairs)
        => "{" + string.Join(",", pairs.Select(p => HexText(p.Key) + ":" + HexText(p.Value))) + "}";

    private static string SlashObject(params (string Key, string JsonOrText)[] pairs)
    {
        var parts = new List<string>(pairs.Length);
        foreach (var pair in pairs)
        {
            var value = pair.JsonOrText is "true" or "false"
                ? pair.JsonOrText
                : PhpCpConfigScripts.Text(pair.JsonOrText);
            parts.Add(PhpCpConfigScripts.Text(pair.Key) + ":" + value);
        }

        return "{" + string.Join(",", parts) + "}";
    }

    /// <summary>PHP <c>json_encode</c> with <c>JSON_UNESCAPED_UNICODE | JSON_HEX_TAG | JSON_HEX_AMP</c>.</summary>
    private static string HexText(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '/': builder.Append("\\/"); break;
                case '<': builder.Append("\\u003C"); break;
                case '>': builder.Append("\\u003E"); break;
                case '&': builder.Append("\\u0026"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
