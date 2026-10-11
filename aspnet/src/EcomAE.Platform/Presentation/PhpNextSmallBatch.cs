using System.Globalization;
using System.Text;
using EcomAE.Platform.Data;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Next small non-ERP PHP includes closed in this slice, verified against PHP 8.3 by
/// <c>Fixtures/NextSmall/golden.json</c>. Function names kept so the inventory can close the files:
/// <c>epc_portal_alias_request_path</c>, <c>epc_portal_alias_canonical_surface_path</c>,
/// <c>epc_portal_alias_redirect_uppercase_surfaces</c>, <c>epc_portal_alias_try_bos_entry</c>,
/// <c>epc_moq_ensure_profile_column</c>, <c>epc_moq_profile_multiplier</c>, <c>epc_moq_effective</c>,
/// <c>epc_fashion_tax_seed_tree</c>, <c>epc_mobile_app_phase1_enabled</c>, <c>epc_mobile_app_landing_html</c>,
/// <c>addItemToDump</c>, <c>epc_phc_h</c>, <c>getInsertedGroups</c>.
/// </summary>
public static class PhpNextSmallBatch
{
    public const string RouteAliasesPath = "content/general_pages/epc_portal_route_aliases.php";
    public const string MoqHelpersPath = "content/shop/pricing/epc_moq_helpers.php";
    public const string FashionTaxonomyPath = "content/shop/price_engine/epc_fashion_taxonomy.php";
    public const string MobileLandingPath = "content/general_pages/epc_mobile_app_landing.php";
    public const string SocialAppPath = "modules/login/epc_social/app.php";
    public const string TreeHelperPath = "content/shop/catalogue/tree_lists/helper.php";
    public const string PricesAjaxInitPath = "cp/content/shop/prices_upload/epc_prices_ajax_init.php";
    public const string MarketingBroadcastPath = "cp/content/control/portal/epc_marketing_broadcast.php";
    public const string MarketingBroadcastConfigPath = "content/general_pages/epc_marketing_broadcast_config.php";
    public const string DataTransferPath = "cp/content/shop/data_transfer/pages/data_transfer.php";
    public const string IlcatsSettingsPath = "content/originalnye-katalogi/settings.php";
    public const string CpActionsAlertPath = "cp/content/control/actions_alert.php";
    public const string DocumentControlGuidePath = "cp/content/shop/document_control/document_control_guide.php";
    public const string AutoxpClicksPath = "autoxp_clicks_control.php";
    public const string CheckAdminAccessPath = "cp/content/control/check_admin_access/check_admin_access.php";
    public const string ChoseCarPath = "content/shop/docpart/car_catalogues/chose_car.php";
    public const string HealthCheckupPath = "cp/content/control/portal/epc_platform_health_checkup.php";
    public const string AboutProgramPath = "cp/content/control/version_control/about_program.php";
    public const string AuthWithUserPath = "cp/content/users/auth_with_user.php";
    public const string PypricesCleanerPath = "cp/content/shop/prices_upload/for_pyprices/pyprices_tables_cleaner.php";
    public const string PageLangMainPath = "cp/content/lang/page_lang_main.php";
    public const string CpLangModulePath = "cp/modules/lang/module.php";
    public const string StorefrontLangModulePath = "modules/lang/module.php";
    public const string UsersHelperPath = "cp/content/users/helper.php";
    public const string IndustryCatalogPrintPath = "content/general_pages/epc_portal_industry_catalog_print.php";

    public const string MarketingBroadcastConfigUrl = "/content/general_pages/epc_marketing_broadcast_config.php";
    public const string AutoxpClicksUrl = "/autoxp_clicks_control.php";
    public const string AuthWithUserUrl = "/cp/content/users/auth_with_user.php";
    public const string PricesAjaxInitUrl = "/cp/content/shop/prices_upload/epc_prices_ajax_init.php";

    public static readonly string[] BosHosts = ["www.ecomae.com", "ecomae.com", "cp.ecomae.com"];

    public const string MoqEnsureColumnSql =
        "ALTER TABLE `epc_price_profiles` ADD `moq_multiplier` DECIMAL(6,2) NOT NULL DEFAULT 1.00 AFTER `margin_percent`;";

    public const string MoqProfileSql =
        "SELECT p.`moq_multiplier`, p.`code` FROM `users_groups_bind` b "
        + "INNER JOIN `epc_price_profiles` p ON p.`group_id` = b.`group_id` "
        + "WHERE b.`user_id` = ? ORDER BY b.`record_id` DESC LIMIT 1;";

    public const string AutoxpSelectSql =
        "SELECT `id`, `clicks_count` FROM `shop_docpart_autoxp_clicks` WHERE `month` = ? AND `year` = ?;";

    public const string AutoxpUpdateSql =
        "UPDATE `shop_docpart_autoxp_clicks` SET `clicks_count` = `clicks_count`+1 WHERE `month` = ? AND `year` = ?;";

    public const string AutoxpInsertSql =
        "INSERT INTO `shop_docpart_autoxp_clicks` (`month`, `year`, `clicks_count`) VALUES (?, ?, ?);";

    public const string PypricesCronLaunchesSql =
        "DELETE FROM `shop_docpart_prices_cron_executor_launches` WHERE `time_start` < ?;";

    public const string PypricesLaunchesSql =
        "DELETE FROM `shop_docpart_pyprices_launches` WHERE `time_start` < ?;";

    public const string PypricesTasksSql =
        "DELETE FROM `shop_docpart_pyprices_tasks` WHERE `time_created` < ?;";

    private static readonly HashSet<string> ScriptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        MarketingBroadcastConfigUrl, AutoxpClicksUrl, AuthWithUserUrl, PricesAjaxInitUrl
    };

    public static bool IsScriptPath(string? path) => path is not null && ScriptPaths.Contains(path);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(MarketingBroadcastConfigUrl, ["GET", "HEAD", "POST"], ServeMarketingBroadcastConfig)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(PricesAjaxInitUrl, ["GET", "HEAD", "POST"], ServePricesAjaxInit)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(AutoxpClicksUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAutoxpAsync(context, connections))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(AuthWithUserUrl, ["GET", "HEAD", "POST"], ServeAuthWithUserGuard)
            .DisableAntiforgery().AllowAnonymous();
    }

    public static string EpcPortalAliasRequestPath(string? uri)
    {
        if (uri is null)
        {
            return "/";
        }

        string path;
        try
        {
            path = new Uri("http://x" + (uri.StartsWith('/') ? uri : "/" + uri)).AbsolutePath;
        }
        catch (UriFormatException)
        {
            return "/";
        }

        if (path.Length == 0)
        {
            return "/";
        }

        path = "/" + path.Replace('\\', '/').Trim('/');
        return path == "//" ? "/" : path;
    }

    public static string? EpcPortalAliasCanonicalSurfacePath(string path)
    {
        if (path.Length < 3)
        {
            return null;
        }

        var slash = path.IndexOf('/', 1);
        var first = (slash < 0 ? path[1..] : path[1..slash]).ToLowerInvariant();
        if (first is not ("cp" or "erp" or "bos"))
        {
            return null;
        }

        var rest = slash < 0 ? "" : path[slash..];
        var canonical = "/" + first + rest;
        return canonical != path ? canonical : null;
    }

    public static (bool Redirected, string? Location) EpcPortalAliasRedirectUppercaseSurfaces(
        string? uri, string? query, bool cli, bool headersSent)
    {
        if (cli || headersSent)
        {
            return (false, null);
        }

        var canonical = EpcPortalAliasCanonicalSurfacePath(EpcPortalAliasRequestPath(uri));
        if (canonical is null)
        {
            return (false, null);
        }

        var suffix = string.IsNullOrEmpty(query) ? "" : "?" + query;
        return (true, canonical + suffix);
    }

    public static (bool Handled, int Status, string? Body) EpcPortalAliasTryBosEntry(string? uri, string? host, bool entryPresent)
    {
        var path = EpcPortalAliasRequestPath(uri);
        if (path != "/bos" && !path.StartsWith("/bos/", StringComparison.Ordinal))
        {
            return (false, 0, null);
        }

        host = (host ?? "").ToLowerInvariant().Trim();
        var colon = host.IndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }

        if (!BosHosts.Contains(host))
        {
            return (true, 404, "Not found.");
        }

        return entryPresent ? (true, 200, null) : (false, 0, null);
    }

    public static double EpcMoqProfileMultiplier(int userId, double? multiplier, string? code)
    {
        if (userId <= 0 || multiplier is null)
        {
            return 1.0;
        }

        var mult = multiplier.Value;
        if (mult <= 0)
        {
            mult = 1.0;
        }

        var profile = (code ?? "").Trim().ToLowerInvariant();
        if (mult == 1.0 && (profile is "retail" or "wholesale"))
        {
            return 1.0;
        }

        return mult;
    }

    public static int EpcMoqEffective(int userId, int baseMoq, double? multiplier, string? code)
    {
        if (baseMoq <= 0)
        {
            baseMoq = 1;
        }

        var mult = EpcMoqProfileMultiplier(userId, multiplier, code);
        if (mult == 1.0)
        {
            return baseMoq;
        }

        return Math.Max(1, (int)Math.Ceiling(baseMoq * mult));
    }

    public static object EpcFashionTaxSeedTree()
        => new object[]
        {
            Branch("fashion-men", "Men", 10, new object[]
            {
                Branch("fashion-men-shirts", "Shirts & polos", null, new object[]
                {
                    Leaf("fashion-men-shirts-casual", "Casual shirts"),
                    Leaf("fashion-men-shirts-formal", "Formal shirts"),
                    Leaf("fashion-men-shirts-polo", "Polos & T-shirts")
                }),
                Branch("fashion-men-trousers", "Trousers & jeans", null, new object[]
                {
                    Leaf("fashion-men-trousers-chinos", "Chinos & formal trousers"),
                    Leaf("fashion-men-trousers-jeans", "Jeans & denim")
                }),
                Leaf("fashion-men-outerwear", "Jackets & outerwear"),
                Branch("fashion-men-footwear", "Footwear", null, new object[]
                {
                    Leaf("fashion-men-footwear-sneakers", "Sneakers & casual"),
                    Leaf("fashion-men-footwear-formal", "Formal shoes")
                }),
                Leaf("fashion-men-activewear", "Activewear & sportswear")
            }),
            Branch("fashion-women", "Women", 20, new object[]
            {
                Branch("fashion-women-dresses", "Dresses & abayas", null, new object[]
                {
                    Leaf("fashion-women-dresses-casual", "Casual dresses"),
                    Leaf("fashion-women-dresses-evening", "Evening & occasion"),
                    Leaf("fashion-women-abayas", "Abayas & kaftans")
                }),
                Leaf("fashion-women-tops", "Tops & blouses"),
                Leaf("fashion-women-trousers", "Trousers & skirts"),
                Branch("fashion-women-footwear", "Footwear", null, new object[]
                {
                    Leaf("fashion-women-footwear-heels", "Heels & pumps"),
                    Leaf("fashion-women-footwear-flats", "Flats & sandals")
                }),
                Leaf("fashion-women-activewear", "Activewear & leggings"),
                Leaf("fashion-women-lingerie", "Lingerie & sleepwear")
            }),
            Branch("fashion-kids", "Kids", 30, new object[]
            {
                Branch("fashion-kids-boys", "Boys", null, new object[]
                {
                    Leaf("fashion-kids-boys-tops", "Tops & T-shirts"),
                    Leaf("fashion-kids-boys-bottoms", "Trousers & shorts")
                }),
                Branch("fashion-kids-girls", "Girls", null, new object[]
                {
                    Leaf("fashion-kids-girls-dresses", "Dresses & sets"),
                    Leaf("fashion-kids-girls-tops", "Tops & skirts")
                }),
                Leaf("fashion-kids-footwear", "Kids footwear"),
                Leaf("fashion-kids-school", "School uniforms")
            }),
            Branch("fashion-accessories", "Accessories", 40, new object[]
            {
                Branch("fashion-accessories-bags", "Bags & wallets", null, new object[]
                {
                    Leaf("fashion-accessories-handbags", "Handbags & totes"),
                    Leaf("fashion-accessories-wallets", "Wallets & card holders")
                }),
                Leaf("fashion-accessories-sunglasses", "Sunglasses"),
                Leaf("fashion-accessories-belts", "Belts & scarves"),
                Leaf("fashion-accessories-watches", "Fashion watches")
            })
        };

    public static bool EpcMobileAppPhase1Enabled(string? host)
        => StorefrontPublicSeo.IsEpartscartHostname(host);

    public static string EpcMobileAppLandingHtml(string? host)
    {
        if (!EpcMobileAppPhase1Enabled(host))
        {
            return "";
        }

        return "\t<section class=\"epc-mobile-app-landing\" style=\"margin:1.5rem 0;padding:1.25rem 1.5rem;border:1px solid #fecaca;border-radius:12px;background:linear-gradient(135deg,#fff5f5,#ffffff);max-width:720px;\">\n"
            + "\t\t<h2 style=\"margin:0 0 .5rem;font-size:1.15rem;color:#991b1b;\">eParts Cart mobile app — Phase 1</h2>\n"
            + "\t\t<p style=\"margin:0 0 .75rem;color:#374151;line-height:1.5;\">\n"
            + "\t\t\tInstall the native shell (Android APK / iOS build) or use <strong>Add to Home Screen</strong> in Chrome or Safari for a full-screen storefront at\n"
            + "\t\t\t<a href=\"/en/\">eParts Cart</a>.\n"
            + "\t\t</p>\n"
            + "\t\t<ul style=\"margin:0;padding-left:1.25rem;color:#4b5563;line-height:1.6;font-size:.95rem;\">\n"
            + "\t\t\t<li>Capacitor app loads <code>www.epartscart.com</code> — same catalog and checkout as the website.</li>\n"
            + "\t\t\t<li>PWA manifest and service worker enable offline notice when the network drops.</li>\n"
            + "\t\t\t<li>Phase 2: push notifications, barcode scan, white-label tenant apps.</li>\n"
            + "\t\t</ul>\n"
            + "\t</section>\n\t";
    }

    public static string SocialApp(bool googleEnabled, string? googleStartUrl, string? tenantKey, string? langHref)
    {
        var returnUrl = Uri.EscapeDataString((langHref ?? "/en/") + "/");
        var googleUrl = "";
        if (googleEnabled)
        {
            googleUrl = StorefrontTinyPages.HtmlSpecialChars(
                (googleStartUrl ?? "/epc-auth-google-start.php")
                + "?context=storefront&tenant_key=" + Uri.EscapeDataString(tenantKey ?? "")
                + "&return_url=" + returnUrl);
        }

        var googleControl = googleEnabled && googleUrl.Length > 0
            ? "\t\t<a class=\"btn btn-ar btn-block epc-cp-auth-google\" href=\"" + googleUrl + "\"><i class=\"fa fa-google\"></i> Continue with Google</a>\n"
            : "\t\t<button type=\"button\" class=\"btn btn-ar btn-block epc-cp-auth-google is-disabled\" disabled><i class=\"fa fa-google\"></i> Continue with Google</button>\n";
        return "<div class=\"epc-cp-auth-modern epc-storefront-auth\">\n"
            + googleControl
            + "\t\t<button type=\"button\" class=\"btn btn-ar btn-block epc-cp-auth-google is-disabled\" disabled title=\"Coming soon\"><i class=\"fa fa-windows\"></i> Microsoft (soon)</button>\n"
            + "\t<button type=\"button\" class=\"btn btn-ar btn-block epc-cp-auth-google is-disabled\" disabled title=\"Coming soon\"><i class=\"fa fa-facebook\"></i> Facebook (soon)</button>\n"
            + "\t<p class=\"epc-cp-auth-hint\">First sign-in creates your customer account automatically.</p>\n"
            + "</div>\n";
    }

    public static List<TreeDumpItem> AddItemToDump(TreeDumpItem item, List<TreeDumpItem> candidateData, object? candidateId)
    {
        if (PhpLooseEquals(item.Parent, candidateId))
        {
            candidateData.Add(item);
            return candidateData;
        }

        foreach (var node in candidateData)
        {
            if (PhpCountZero(node.Count))
            {
                continue;
            }

            node.Data = AddItemToDump(item, node.Data, node.Id);
        }

        return candidateData;
    }

    public static string PricesAjaxInitNoDb()
        => "{\"status\":false,\"message\":\"No DB Connect\"}";

    public static string MarketingBroadcast(bool panelPresent, string includeBody)
        => panelPresent
            ? includeBody
            : "<div class=\"alert alert-danger\">Marketing broadcast panel missing. Deploy <code>"
                + "cp/content/control/portal/epc_marketing_broadcast" + "_panel.php</code>.</div>";

    public static string MarketingBroadcastConfig(string? shopName, string? shopUrl)
        => "window.EPC_MB=" + "{"
            + PhpCpConfigScripts.Text("ajaxUrl") + ":" + PhpCpConfigScripts.Text("/content/general_pages/ajax_epc_marketing_broadcast.php") + ","
            + PhpCpConfigScripts.Text("shopName") + ":" + PhpCpConfigScripts.Text(shopName ?? "Your shop") + ","
            + PhpCpConfigScripts.Text("shopUrl") + ":" + PhpCpConfigScripts.Text(shopUrl ?? "/")
            + "};";

    public static string MarketingBroadcastConfigEmptyDoc() => "window.EPC_MB={};";

    public static string DataTransferHub(string? backendDir, string? template, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        var backend = backendDir ?? "cp";
        var images = "/" + backend + "/templates/" + (template ?? "bootstrap_admin") + "/images/";
        const string N = "\r\n";
        return N + N + N + "<div class=\"col-lg-12\">" + N + "\t<div class=\"hpanel\">" + N + "\t\t<div class=\"panel-heading hbuilt\">" + N + "\t\t\t"
            + T(3161) + "\t\t</div>" + N + "\t\t<div class=\"panel-body\">" + N + "\t\t\t<a class=\"panel_a\" href=\"/"
            + backend + "/shop/perenos-dannyx/eksport-kataloga-tovarov-v-xml-i-json\">" + N + "\t\t\t\t"
            + "<div class=\"panel_a_img\" style=\"background: url('" + images + "catalogue_export.png') 0 0 no-repeat;\"></div>" + N + "\t\t\t\t"
            + "<div class=\"panel_a_caption\">" + T(3229) + "</div>" + N + "\t\t\t</a>" + N + "\t\t\t" + N + "\t\t\t"
            + "<!--" + N + "\t\t\t<a class=\"panel_a\" href=\"/" + backend + "/shop/perenos-dannyx/vygruzka-na-yandeksmarket\">" + N
            + "\t\t\t\t<div class=\"panel_a_img\" style=\"background: url('" + images + "yml.png') 0 0 no-repeat;\"></div>" + N
            + "\t\t\t\t<div class=\"panel_a_caption\">" + T(3230) + "</div>" + N + "\t\t\t</a>" + N + "\t\t\t-->" + N + "\t\t\t" + N + "\t\t\t"
            + "<a class=\"panel_a\" href=\"/" + backend + "/shop/perenos-dannyx/import-kataloga-tovarov-iz-xml-i-json\">" + N + "\t\t\t\t"
            + "<div class=\"panel_a_img\" style=\"background: url('" + images + "catalogue_import.png') 0 0 no-repeat;\"></div>" + N + "\t\t\t\t"
            + "<div class=\"panel_a_caption\">" + T(3231) + "</div>" + N + "\t\t\t</a>" + N + "\t\t\t" + N + "\t\t\t" + N + "\t\t\t"
            + "<a class=\"panel_a\" href=\"/" + backend + "/shop/perenos-dannyx/import-tovarov-v-katalog-iz-csv\">" + N + "\t\t\t\t"
            + "<div class=\"panel_a_img\" style=\"background: url('" + images + "catalogue_import_csv.png') 0 0 no-repeat;\"></div>" + N + "\t\t\t\t"
            + "<div class=\"panel_a_caption\">" + T(3232) + "</div>" + N + "\t\t\t</a>" + N + "\t\t\t" + N + "\t\t\t" + N + "\t\t\t"
            + "<a class=\"panel_a\" href=\"/" + backend + "\">" + N + "\t\t\t\t"
            + "<div class=\"panel_a_img\" style=\"background: url('/" + backend + "/templates/" + (template ?? "bootstrap_admin")
            + "/images/power_off.png') 0 0 no-repeat;\"></div>" + N + "\t\t\t\t"
            + "<div class=\"panel_a_caption\">" + T(2116) + "</div>" + N + "\t\t\t</a>" + N + "\t\t</div>" + N + "\t</div>" + N + "</div>";
    }

    public static IReadOnlyDictionary<string, object?> IlcatsSettings(string langHrefNoSlash, object? partInfo, string? remoteAddr)
        => new Dictionary<string, object?>
        {
            ["apiClientId"] = 15652,
            ["apiKey"] = "2fc9e2385b7e5270ade11c165ca84c8b",
            ["apiDomain"] = "incar62.ru",
            ["apiStaticContentHost"] = "//static.ilcats.ru",
            ["apiImagesHost"] = "//images.ilcats.ru",
            ["apiVersion"] = "2.0",
            ["apiArticlePartLink"] = "http://incar62.ru/" + langHrefNoSlash + "/shop/part_search?article=<%API_URL_PART_NUMBER%>",
            ["apiArticlePartLinkTarget"] = 1,
            ["apiPartWBrandLink"] = "http://incar62.ru/" + langHrefNoSlash + "/shop/part_search?article=<%API_URL_PART_NUMBER%>",
            ["apiPartWBrandLinkTarget"] = 1,
            ["apiPartUsageTarget"] = 0,
            ["apiPartInfo"] = partInfo,
            ["apiClientIpAddress"] = remoteAddr,
            ["apiHttpCatalogsPath"] = "originalnye-katalogi",
            ["apiActiveLanguages"] = new[] { "ru" }
        };

    public static string CpActionsAlert(Func<string, string?> query, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        var sb = new StringBuilder("\n");
        AppendCpAlert(sb, query("success_message"), "success_div", "alert-success", T(2383));
        AppendCpAlert(sb, query("error_message"), "danger_div", "alert-danger", T(2384));
        AppendCpAlert(sb, query("warning_message"), "purple_div", "alert-purple", T(2385));
        AppendCpAlert(sb, query("info_message"), "info_div", "alert-info", T(2386));
        sb.Append('\n');
        return sb.ToString();
    }

    public static string DocumentControlGuide()
        => "\n<div class=\"well\">\n"
            + "\t<h3><i class=\"fa fa-book\"></i> Document Control System — Guide</h3>\n"
            + "\t<p>Industrial-grade English document management for e-world Commerce System. Replaces the legacy Russian print module with FTA-ready templates and attachment storage.</p>\n"
            + "</div>\n\n"
            + "<h4>1. Company profile</h4>\n<ol>\n"
            + "\t<li>Open <strong>Company profile</strong> and enter legal name, full address, <strong>TRN</strong> (15-digit UAE VAT registration), phone, email, and bank IBAN.</li>\n"
            + "\t<li>Upload your <strong>company logo</strong> (PNG/JPG). It appears on every printed document.</li>\n"
            + "\t<li>Set the <strong>Legal footer</strong> — FTA retention notice, terms, and disclaimers shown on all documents.</li>\n"
            + "\t<li>Optional: click <strong>Import from E-Invoicing</strong> to copy seller details already configured in ERP → E-Invoicing.</li>\n"
            + "</ol>\n\n<h4>2. Document templates</h4>\n<p>Four default templates are pre-installed:</p>\n<ul>\n"
            + "\t<li><strong>FTA Tax Invoice</strong> — mandatory fields for UAE VAT: supplier TRN, buyer TRN (if registered), invoice number &amp; date, line-level net/VAT/total, amount in words.</li>\n"
            + "\t<li><strong>Packing Slip</strong> — warehouse picking list (no tax amounts).</li>\n"
            + "\t<li><strong>Delivery Note</strong> — customer sign-off block for proof of delivery.</li>\n"
            + "\t<li><strong>Payment Receipt</strong> — records amount received, method, and reference.</li>\n"
            + "</ul>\n<p>Templates use HTML with placeholders such as <code>{{company_trn}}</code>, <code>{{lines_table}}</code>, <code>{{legal_footer}}</code>. Edit header, body, footer, and CSS directly — changes apply immediately to new prints.</p>\n\n"
            + "<h4>3. Printing documents</h4>\n<ol>\n"
            + "\t<li>Go to <strong>Print documents</strong>.</li>\n"
            + "\t<li>Find the order and click the document type (opens in a new tab).</li>\n"
            + "\t<li>Use the browser <strong>Print</strong> button or Ctrl+P. Save as PDF for email/archive.</li>\n"
            + "</ol>\n<p>Invoice numbers prefer the e-invoice number from ERP if one exists; otherwise format <code>INV-000123</code>.</p>\n\n"
            + "<h4>4. Supplier &amp; other attachments</h4>\n<ol>\n"
            + "\t<li>Open <strong>Attachments</strong>.</li>\n"
            + "\t<li>Enter order ID, choose category (e.g. <strong>Supplier purchase invoice</strong>), supplier name, reference, and upload PDF/image.</li>\n"
            + "\t<li>Files are stored securely and linked to the order for audit trail (input VAT support).</li>\n"
            + "</ol>\n\n<h4>5. FTA compliance checklist</h4>\n"
            + "<table class=\"table table-bordered\">\n\t<thead><tr><th>Requirement</th><th>Where configured</th></tr></thead>\n\t<tbody>\n"
            + "\t\t<tr><td>Supplier TRN on tax invoice</td><td>Company profile → TRN</td></tr>\n"
            + "\t\t<tr><td>Buyer TRN (if VAT registered)</td><td>Customers → E-invoice buyer profile</td></tr>\n"
            + "\t\t<tr><td>Unique invoice number &amp; date</td><td>Auto from order / e-invoice</td></tr>\n"
            + "\t\t<tr><td>VAT rate &amp; amount</td><td>Finance → VAT settings (default 5%)</td></tr>\n"
            + "\t\t<tr><td>Line item description &amp; value</td><td>Order lines</td></tr>\n"
            + "\t\t<tr><td>5-year record retention</td><td>Legal footer + attachment storage</td></tr>\n"
            + "\t</tbody>\n</table>\n\n<h4>6. Legacy module</h4>\n"
            + "<p>The old Russian module (<code>/cp/shop/modul-pechati-dokumentov</code>) redirects to this panel. Russian TORG-12 / UPD forms remain in the database for reference but are not recommended for UAE operations.</p>\n\n"
            + "<h4>7. Support &amp; deployment</h4>\n"
            + "<p>Setup script (server): <code>/epc-document-control-cp-setup.php?token=…</code></p>\n"
            + "<p>Designed by <strong>Electronic World Group</strong> for the <strong>e-world Commerce System</strong>.</p>\n";

    public static string AutoxpNoDb() => "No DB connect";

    public static string AutoxpClicks(int existingCount)
        => existingCount >= 2000 ? "0" : "1";

    public static string CheckAdminAccessJson(bool pagesPresent, bool allowed, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        if (!pagesPresent)
        {
            return "{\"status\":false,\"error\":" + PhpCpTinyPages.PhpDefaultJson(T(2387))
                + ",\"message\":" + PhpCpTinyPages.PhpDefaultJson(T(2387)) + "}";
        }

        if (!allowed)
        {
            return "{\"status\":false,\"error\":" + PhpCpTinyPages.PhpDefaultJson(T(2388))
                + ",\"message\":" + PhpCpTinyPages.PhpDefaultJson(T(2388)) + "}";
        }

        return "";
    }

    public static string ChoseCar(string langHref, IReadOnlyList<(int Id, string Image, object Caption)> cars, Func<object, string>? translate = null)
    {
        string T(object id) => (translate ?? (key => "{" + Convert.ToString(key, CultureInfo.InvariantCulture) + "}"))(id);
        var sb = new StringBuilder("\n\n\n\n\n<ul class=\"bs-glyphicons-list\">\n");
        foreach (var car in cars)
        {
            sb.Append("\t<li>\n\t\t<a href=\"").Append(langHref).Append("/shop/avtomobilnye-katalogi/vybor-kataloga?car_id=")
                .Append(car.Id.ToString(CultureInfo.InvariantCulture)).Append("\">\n\t\t\t<img src=\"/content/files/images/car_logos/")
                .Append(car.Image).Append("\" /><br>\n\t\t\t<span class=\"glyphicon-class\">").Append(T(car.Caption))
                .Append("</span>\n\t\t</a>\n\t</li>\n");
        }

        return sb.Append("    </ul>").ToString();
    }

    public static string EpcPhcH(object? value) => StorefrontTinyPages.HtmlSpecialChars(Convert.ToString(value, CultureInfo.InvariantCulture));

    public static string PlatformHealthCheckup(bool superHost, string token)
    {
        if (!superHost)
        {
            return "<div class=\"alert alert-warning\">Platform health checkup is available on <strong>Super CP</strong> (www.ecomae.com) only.</div>";
        }

        _ = "https://www.ecomae.com/epc-platform-health-checkup-api.php?token=" + Uri.EscapeDataString(token);
        return "<div class=\"epc-portal-settings epc-phc\" id=\"epc_phc_root\">\n\t<div class=\"hpanel\">\n\t\t<div class=\"panel-heading\">\n"
            + "\t\t\t<h2><i class=\"fas fa-heartbeat\"></i> Platform health checkup</h2>\n"
            + "\t\t\t<p class=\"text-muted\">Tenant URLs, SSL, ERP DB isolation, nginx, backups, indexing, and VPS hints — run on demand for go-live and weekly ops.</p>\n"
            + "\t\t</div>\n\t\t<div class=\"panel-body\">\n\t\t\t<div class=\"epc-phc__toolbar\">\n"
            + "\t\t\t\t<button type=\"button\" class=\"btn btn-primary\" id=\"epc_phc_run\"><i class=\"fas fa-play\"></i> Run checkup</button>\n"
            + "\t\t\t\t<button type=\"button\" class=\"btn btn-default\" id=\"epc_phc_export_csv\"><i class=\"fas fa-download\"></i> Export CSV</button>\n"
            + "\t\t\t\t<span class=\"epc-phc__status\" id=\"epc_phc_last_run\">Last run: —</span>\n"
            + "\t\t\t\t<span id=\"epc_phc_loading\"><i class=\"fas fa-spinner fa-spin\"></i> Running probes…</span>\n"
            + "\t\t\t</div>\n\t\t\t<div id=\"epc_phc_results\">\n"
            + "\t\t\t\t<p class=\"text-muted\">Click <strong>Run checkup</strong> to load the latest report from the platform API.</p>\n"
            + "\t\t\t</div>\n\t\t</div>\n\t</div>\n</div>\n";
    }

    public static string AboutProgram(string? baseVersion, long baseTime, string? currentVersion, long currentTime, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        const string N = "\r\n";
        return N + "<div class=\"col-lg-12\">" + N + "\t<div class=\"hpanel\">" + N + "\t\t<div class=\"panel-heading hbuilt\">" + N + "\t\t\t"
            + T(2492) + "\t\t</div>" + N + "\t\t<div class=\"panel-body\">" + N + "\t\t\t<div class=\"table-responsive\">" + N
            + "\t\t\t\t<table cellpadding=\"1\" cellspacing=\"1\" class=\"table table-bordered table-striped\">" + N
            + "\t\t\t\t\t<tbody>" + N + "\t\t\t\t\t<tr>" + N + "\t\t\t\t\t\t<td><b>" + T(2493) + "</b></td>" + N + "\t\t\t\t\t\t<td>"
            + baseVersion + "</td>" + N + "\t\t\t\t\t\t<td><b>" + T(2494) + "</b></td>" + N + "\t\t\t\t\t\t<td>"
            + PhpDate(baseTime) + "</td>" + N + "\t\t\t\t\t</tr>" + N + "\t\t\t\t\t<tr>" + N + "\t\t\t\t\t\t<td><b>" + T(2495)
            + "</b></td>" + N + "\t\t\t\t\t\t<td>" + currentVersion + "</td>" + N + "\t\t\t\t\t\t<td><b>" + T(2494)
            + "</b></td>" + N + "\t\t\t\t\t\t<td>" + PhpDate(currentTime) + "</td>" + N + "\t\t\t\t\t</tr>" + N + "\t\t\t\t\t</tbody>" + N
            + "\t\t\t\t</table>" + N + "\t\t\t</div>" + N + N + "\t\t</div>" + N + "\t</div>" + N + "</div>";
    }

    public static string AuthWithUserNoDb() => "No DB connect";

    public static string AuthWithUserForbidden()
        => "{\"status\":false,\"message\":\"Forbidden\"}";

    public static string AuthWithUserOk()
        => "{\"status\":true,\"message\":\"Ok\"}";

    public static string AuthWithUserSession(object? userId, long time, string secret, string remoteAddr)
        => Md5Hex(Convert.ToString(userId, CultureInfo.InvariantCulture) + time.ToString(CultureInfo.InvariantCulture) + secret + "ok" + remoteAddr);

    public static string AuthWithUserCsrf(string secret, string session, string remoteAddr, string userAgent)
        => Sha1Hex(secret + session + remoteAddr + userAgent);

    public static long PypricesWeekAgo(long now) => now - 604800;

    public static string PageLangMain(string? backendDir, string? template, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        var backend = backendDir ?? "cp";
        return "\t<div class=\"col-lg-12\">\n\t\t<div class=\"hpanel\">\n\t\t\t<div class=\"panel-heading hbuilt\">\n\t\t\t\t"
            + T(2113) + "\n\t\t\t</div>\n\t\t\t<div class=\"panel-body\">\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t"
            + BackendButton("#8e44ad", "fas fa-tasks", T(2539), "/" + backend + "/lang/configurator")
            + "\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t"
            + BackendButton("#00b05a", "fas fa-pencil-alt", T(2528), "/" + backend + "/lang/editor")
            + "\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t"
            + "<a class=\"panel_a\" href=\"/" + backend + "\">\n\t\t\t\t\t"
            + "<div class=\"panel_a_img\" style=\"background: url('/" + backend + "/templates/" + (template ?? "bootstrap_admin")
            + "/images/power_off.png') 0 0 no-repeat;\"></div>\n\t\t\t\t\t"
            + "<div class=\"panel_a_caption\">" + T(2116) + "</div>\n\t\t\t\t</a>\n\t\t\t</div>\n\t\t</div>\n\t</div>\n\t";
    }

    public static string CpLangModule(bool multilang, IReadOnlyList<(string Code, object Caption)> langs, string currentLang, Func<object, string, string>? translate = null)
    {
        if (!multilang || langs.Count <= 1)
        {
            return "";
        }

        string T(object key, string lang) => (translate ?? ((k, _) => "{" + Convert.ToString(k, CultureInfo.InvariantCulture) + "}"))(key, lang);
        var sb = new StringBuilder("\t\t<div class=\"text-center\" style=\"padding:5px;border-top:1px solid #e4e5e7;\">\n\t\t\t")
            .Append("{3993}:\n\t\t\t<select class=\"form-control\" onchange=\"lang_selected(this.value);\">\n");
        foreach (var lang in langs)
        {
            var selected = lang.Code == currentLang ? " selected=\"selected\" " : "";
            sb.Append("\t\t\t\t\t<option value=\"").Append(lang.Code).Append("\" ").Append(selected).Append('>')
                .Append(lang.Code.ToUpperInvariant()).Append(" | ").Append(T(lang.Caption, lang.Code)).Append("</option>\n");
        }

        return sb.Append("\t\t\t</select>\n\t\t</div>\n\t\t\n\t\t<script>\n\t\t//На случай, если модуль подключается в нескольких местах\n")
            .Append("\t\tif( typeof lang_selected != 'function' )\n\t\t{\n\t\t\twindow.lang_selected = function(lang)\n\t\t\t{\n")
            .Append("\t\t\t\t//Записать в куки язык\n\t\t\t\tvar date = new Date(new Date().getTime() + 15552000 * 1000);\n")
            .Append("\t\t\t\tdocument.cookie = \"lang_cp=\"+lang+\"; path=/; expires=\" + date.toUTCString();\n\t\t\t\t\n")
            .Append("\t\t\t\t//Перезагрузить страницу\n\t\t\t\tlocation = location;\n\t\t\t};\n\t\t}\n\t\t</script>\n\t\t\n\t\t").ToString();
    }

    public static string StorefrontLangModule(
        bool multilang,
        IReadOnlyList<(string Code, object Caption)> langs,
        string currentLang,
        string pageUrlWithLangTag,
        Func<object, string, string>? translate = null)
    {
        if (!multilang || langs.Count <= 1)
        {
            return "";
        }

        string T(object key, string lang) => (translate ?? ((k, _) => "{" + Convert.ToString(k, CultureInfo.InvariantCulture) + "}"))(key, lang);
        var sb = new StringBuilder("\t\t<select onchange=\"lang_selected(this.value);\" style=\"border:0;width:40px;background:transparent;\" class=\"lang_select\">\n\t\t");
        foreach (var lang in langs)
        {
            var selected = lang.Code == currentLang ? " selected=\"selected\" " : "";
            sb.Append("\t<option value=\"").Append(lang.Code).Append("\" ").Append(selected).Append('>')
                .Append(lang.Code.ToUpperInvariant()).Append(" | ").Append(T(lang.Caption, lang.Code)).Append("</option>\n\t\t");
        }

        return sb.Append("</select>\n\t\t\n\t\t\n\t\t\n\t\t<script>\n")
            .Append("\t//На случай, если модуль подключается в нескольких местах\n")
            .Append("\tif( typeof lang_selected != 'function' )\n\t{\n")
            .Append("\t\twindow.lang_selected = function(lang)\n\t\t{\n")
            .Append("\t\t\tlang = String(lang || '').toLowerCase().replace(/[^a-z\\-]/g, '');\n")
            .Append("\t\t\tif (!lang) {\n\t\t\t\treturn;\n\t\t\t}\n")
            .Append("\t\t\t//Записать в куки язык\n")
            .Append("\t\t\tvar date = new Date(new Date().getTime() + 15552000 * 1000);\n")
            .Append("\t\t\tdocument.cookie = \"lang=\"+lang+\"; path=/; expires=\" + date.toUTCString();\n")
            .Append("\t\t\t\n")
            .Append("\t\t\t//Перейти на URL этой же страницы, но, для другого языка\n")
            .Append("\t\t\tvar page_url_with_lang_tag = '").Append(pageUrlWithLangTag).Append("';\n")
            .Append("\t\t\tvar target = '';\n")
            .Append("\t\t\tif (page_url_with_lang_tag && page_url_with_lang_tag.indexOf('<lang>') !== -1) {\n")
            .Append("\t\t\t\ttarget = page_url_with_lang_tag.replace('<lang>', lang);\n")
            .Append("\t\t\t} else {\n")
            .Append("\t\t\t\t// Fallback: swap first path segment when it looks like a language code\n")
            .Append("\t\t\t\tvar path = window.location.pathname || '/';\n")
            .Append("\t\t\t\tvar search = window.location.search || '';\n")
            .Append("\t\t\t\tvar hash = window.location.hash || '';\n")
            .Append("\t\t\t\tvar parts = path.split('/');\n")
            .Append("\t\t\t\t// path like /en/parts/... → [\"\", \"en\", \"parts\", ...]\n")
            .Append("\t\t\t\tif (parts.length > 1 && /^[a-z]{2}(?:-[a-zA-Z]+)?$/i.test(parts[1] || '')) {\n")
            .Append("\t\t\t\t\tparts[1] = lang;\n")
            .Append("\t\t\t\t\ttarget = parts.join('/') + search + hash;\n")
            .Append("\t\t\t\t} else {\n")
            .Append("\t\t\t\t\ttarget = '/' + lang + (path === '/' ? '/' : path) + search + hash;\n")
            .Append("\t\t\t\t}\n")
            .Append("\t\t\t}\n")
            .Append("\t\t\tif (target) {\n")
            .Append("\t\t\t\twindow.location.assign(target);\n")
            .Append("\t\t\t}\n")
            .Append("\t\t};\n\t}\n</script>\n\t\t\n\t\t\n\t\t").ToString();
    }

    public static List<int> GetInsertedGroups(int root, IReadOnlyDictionary<int, (int Count, IReadOnlyList<int> Children)> tree)
    {
        var oneRoot = new List<int> { root };
        WalkInserted(root, tree, oneRoot);
        return oneRoot;
    }

    public static string IndustryCatalogPrint(string? profile, string title, bool hasItems, string renderBody)
    {
        if (profile is null || !hasItems)
        {
            return "";
        }

        return "<div class=\"col-lg-12 epc-goods-catalog epc-goods-catalog--" + StorefrontTinyPages.HtmlSpecialChars(profile)
            + "\">\n\t<h2 class=\"section-title\">" + StorefrontTinyPages.HtmlSpecialChars(title) + "</h2>\n\t"
            + renderBody + "</div>\n";
    }

    private static void WalkInserted(int group, IReadOnlyDictionary<int, (int Count, IReadOnlyList<int> Children)> tree, List<int> oneRoot)
    {
        if (!tree.TryGetValue(group, out var node) || node.Count == 0)
        {
            return;
        }

        foreach (var child in node.Children)
        {
            if (!oneRoot.Contains(child))
            {
                oneRoot.Add(child);
            }

            WalkInserted(child, tree, oneRoot);
        }
    }

    private static string BackendButton(string color, string icon, string caption, string url)
        => "\t\t<a class=\"panel_a\" href=\"" + url + "\"  >\n"
            + "\t\t\t<div class=\"panel_a_img\" style=\"background-color: " + color
            + ";width:96px;height:96px;display:table-cell;vertical-align:middle;\"><i class=\"" + icon
            + "\" style=\"color:#FFF;font-size:45px\"></i></div>\n"
            + "\t\t\t<div class=\"panel_a_caption\">" + caption + "</div>\n\t\t</a>\n\t\t";

    private static void AppendCpAlert(StringBuilder sb, string? value, string id, string css, string strong)
    {
        if (string.IsNullOrEmpty(value) || value == "0")
        {
            return;
        }

        sb.Append("\t<div class=\"col-lg-12\" id=\"").Append(id).Append("\">\n")
            .Append("\t\t<div class=\"alert ").Append(css).Append(" fade in\">\n")
            .Append("\t\t\t<button class=\"close\" onclick=\"clearAlert('").Append(id).Append("');\" data-dismiss=\"alert\"><span>×</span></button>\n")
            .Append("\t\t\t<strong>").Append(strong).Append("</strong> ").Append(PhpHtmlEntities.Encode(value)).Append("\t\t</div>\n")
            .Append("\t</div>\n    ");
    }

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

    private static bool PhpLooseEquals(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (TryNumber(left, out var a) && TryNumber(right, out var b))
        {
            return a == b;
        }

        return string.Equals(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private static bool TryNumber(object? value, out double number)
    {
        switch (value)
        {
            case null:
                number = 0;
                return true;
            case bool flag:
                number = flag ? 1 : 0;
                return true;
            case sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal:
                number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;
            case string text:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            default:
                number = 0;
                return false;
        }
    }

    private static bool PhpCountZero(object? count)
        => TryNumber(count, out var n) && n == 0;

    private static string PhpDate(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Md5Hex(string value)
    {
        var hash = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Sha1Hex(string value)
    {
        var hash = System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static Task ServeMarketingBroadcastConfig(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return response.WriteAsync(MarketingBroadcastConfig("Your shop", "/"), context.RequestAborted);
    }

    private static Task ServePricesAjaxInit(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "application/json; charset=utf-8";
        return response.WriteAsync(PricesAjaxInitNoDb(), context.RequestAborted);
    }

    private static Task ServeAuthWithUserGuard(HttpContext context)
    {
        // GET must not mint a guest or impersonation session. PHP only writes on an admin POST.
        var response = context.Response;
        response.ContentType = "application/json;charset=utf-8;";
        return response.WriteAsync(AuthWithUserForbidden(), context.RequestAborted);
    }

    private static async Task ServeAutoxpAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        var response = context.Response;
        if (!connections.IsConfigured)
        {
            await response.WriteAsync(AutoxpNoDb(), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        try
        {
            var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var month = now.Month;
            var year = now.Year;
            await using var select = connection.CreateCommand();
            select.CommandText = EcomAE.Platform.Erp.ErpDb.Positional(AutoxpSelectSql);
            EcomAE.Platform.Erp.ErpDb.AddParameters(select, month, year);
            await using var reader = await select.ExecuteReaderAsync(context.RequestAborted).ConfigureAwait(false);
            if (await reader.ReadAsync(context.RequestAborted).ConfigureAwait(false))
            {
                var count = Convert.ToInt32(reader["clicks_count"], CultureInfo.InvariantCulture);
                await reader.DisposeAsync().ConfigureAwait(false);
                if (count >= 2000)
                {
                    await response.WriteAsync(AutoxpClicks(count), context.RequestAborted).ConfigureAwait(false);
                    return;
                }

                await using var update = connection.CreateCommand();
                update.CommandText = EcomAE.Platform.Erp.ErpDb.Positional(AutoxpUpdateSql);
                EcomAE.Platform.Erp.ErpDb.AddParameters(update, month, year);
                await update.ExecuteNonQueryAsync(context.RequestAborted).ConfigureAwait(false);
                await response.WriteAsync("1", context.RequestAborted).ConfigureAwait(false);
                return;
            }

            await reader.DisposeAsync().ConfigureAwait(false);
            await using var insert = connection.CreateCommand();
            insert.CommandText = EcomAE.Platform.Erp.ErpDb.Positional(AutoxpInsertSql);
            EcomAE.Platform.Erp.ErpDb.AddParameters(insert, month, year, 1);
            await insert.ExecuteNonQueryAsync(context.RequestAborted).ConfigureAwait(false);
            await response.WriteAsync("1", context.RequestAborted).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            await response.WriteAsync(AutoxpNoDb(), context.RequestAborted).ConfigureAwait(false);
        }
    }

    public sealed class TreeDumpItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public object? Id { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("parent")]
        public object? Parent { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("count")]
        public object? Count { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<TreeDumpItem> Data { get; set; } = [];
    }
}
