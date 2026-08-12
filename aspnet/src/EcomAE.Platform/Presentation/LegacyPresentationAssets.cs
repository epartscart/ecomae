namespace EcomAE.Platform.Presentation;

/// <summary>
/// Asset URLs that match the live PHP chrome so ASP.NET Core shells reuse the same CSS/JS presentation.
/// Paths intentionally point at the existing PHP/static pipeline (epc-static.php + content/general_pages CSS helpers).
/// </summary>
public static class LegacyPresentationAssets
{
    public const string BrandName = "ECOM AE";
    /// <summary>ECOM AE mark — /platform-assets survives PHP pause on www.</summary>
    public const string BrandMarkUrl = "/platform-assets/ecomae-mark.svg";

    public static IReadOnlyList<string> StylesheetsFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => ControlPanelStylesheets,
        "erp" => ErpStylesheets,
        "bos" => BosStylesheets,
        // IP reuses BOS shell chrome; LifeOS uses self-contained page CSS in HeadContent.
        "ip" => BosStylesheets,
        "lifeos" => LifeOsStylesheets,
        "storefront" => StorefrontStylesheets,
        "marketing" => MarketingStylesheets,
        _ => ControlPanelStylesheets
    };

    public static readonly IReadOnlyList<string> ControlPanelStylesheets =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css",
        "https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/css/bootstrap.min.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/css/astself.css",
        "/content/general_pages/epc_cp_ui_css.php",
        "/content/general_pages/epc_cp_professional_css.php",
        "/content/general_pages/epc_cp_density_css.php",
        "/content/general_pages/epc_cp_tenant_polish_css.php",
        "/content/general_pages/epc_cp_storefront_topbar_css.php",
        // Tenant Control Command Centre (matches live PHP /CP/ dashboard presentation)
        "/content/general_pages/epc_cp_command_dashboard_css.php",
        // After professional: neutralize invented digest heroes → PHP epc-scp-* module look
        // (Super CP + Tenant CP). /platform-assets survives PHP pause.
        "/platform-assets/epc_cp_aspnet_module_parity.css?v=20260811scp",
        "/content/general_pages/epc_cp_aspnet_module_parity_css.php"
    ];

    /// <summary>
    /// Animated cart mark for &lt;img src&gt;. Served by ASP.NET <c>/platform-assets</c>
    /// (PHP library dies with "No access" outside _ASTEXE_ / when PHP serving is paused).
    /// </summary>
    public const string EpartsCartMarkUrl = "/platform-assets/eparts-animated-cart-mark.svg";

    /// <summary>Renderable animated cart logo fragment (HTML/SVG), ASP.NET-bridged.</summary>
    public const string AnimatedEpartsCartFragmentUrl = "/platform-assets/eparts-animated-cart-fragment.html";

    /// <summary>
    /// Lean BOS-parity login CSS for /cp/login and /erp/login (same shell as /bos/login).
    /// Atmosphere tint only differs via epc_bos_login_surface_accents.css.
    /// Accents + tenant brand use /platform-assets so they survive PHP pause and stale www trees.
    /// </summary>
    public static readonly IReadOnlyList<string> LoginStylesheets =
    [
        "/epc-static.php?f=bos/epc_bos_shell.css",
        "/platform-assets/epc_bos_login_surface_accents.css?v=20260807a",
        // Tenant animated cart + catalog brand logos on /cp/login & /erp/login
        "/platform-assets/eparts-animated-logo.css",
        "/platform-assets/epc_portal_tenant_brand.css"
    ];

    /// <summary>ERP login uses the same BOS-parity shell (accents + tenant brand assets).</summary>
    public static readonly IReadOnlyList<string> ErpLoginStylesheets =
    [
        "/epc-static.php?f=bos/epc_bos_shell.css",
        "/platform-assets/epc_bos_login_surface_accents.css?v=20260807a",
        "/platform-assets/eparts-animated-logo.css",
        "/platform-assets/epc_portal_tenant_brand.css"
    ];

    /// <summary>BOS login matrix/particle JS (PHP bos/epc_bos_shell.js).</summary>
    public static readonly IReadOnlyList<string> BosLoginScripts =
    [
        "/epc-static.php?f=bos/epc_bos_shell.js"
    ];

    public static readonly IReadOnlyList<string> ErpStylesheets =
    [
        "/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fontawesome/css/font-awesome.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/css/bootstrap.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css",
        // Match erp_desktop.php stylesheet order (portal + ui + professional + CP blue theme)
        "/content/shop/finance/epc_erp_portal_css.php",
        "/content/shop/finance/epc_erp_ui_css.php",
        "/content/shop/finance/epc_erp_professional_css.php",
        "/epc-static.php?f=cp/content/shop/finance/erp/theme/erp_theme.css",
        "/epc-static.php?f=cp/content/shop/finance/erp/theme/erp_dashboard_premium.css",
        "/content/general_pages/epc_cp_ui_css.php",
        "/content/general_pages/epc_cp_professional_css.php",
        // After professional: neutralize ASP.NET digest heroes → PHP page-hd / kpi / table-epc look.
        // /platform-assets survives PHP pause on Super / Tenant / ERP-only hosts.
        "/platform-assets/epc_erp_aspnet_module_parity.css?v=20260811erp",
        "/content/shop/finance/epc_erp_aspnet_module_parity_css.php"
    ];

    public static readonly IReadOnlyList<string> BosStylesheets =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css",
        "/epc-static.php?f=bos/epc_bos_shell.css"
    ];

    /// <summary>LifeOS marketing/app — Font Awesome only; page CSS is inline in HeadContent.</summary>
    public static readonly IReadOnlyList<string> LifeOsStylesheets =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css"
    ];

    public static readonly IReadOnlyList<string> StorefrontStylesheets =
    [
        // epartscart.com live theme is nero (templates/nero/desktop.php), not modex.
        "/epc-static.php?f=templates/nero/assets/css/style_all.css",
        "/epc-static.php?f=templates/nero/css/astself.css",
        "/epc-static.php?f=templates/nero/css/catalogue/catalogue.css",
        "/epc-static.php?f=templates/nero/css/docpart/style.css",
        "/modules/slider/css/style.css",
        // Animated eparts cart logo (PHP enqueue equivalent; also covered by professional shell CSS)
        "/platform-assets/eparts-animated-logo.css",
        // PHP site_professional_shell polish — prefer epc-static gateway (works when www has the file)
        "/epc-static.php?f=content/general_pages/epc_storefront_professional_shell.css",
        "/content/general_pages/epc_storefront_professional_shell.css",
        // Container width (98% / 1728px) + piston hero (requires html data-epc-industry/storefront attrs)
        "/content/general_pages/epc_automotive_spareparts.css",
        // ASP.NET module digests → PHP page-header look (no invented gradient heroes)
        "/platform-assets/epc_storefront_aspnet_module_parity.css?v=20260809php",
        "/content/general_pages/epc_storefront_aspnet_module_parity.css"
    ];

    /// <summary>
    /// www.ecomae.com marketing chrome — animated epm-hub hero + home sections (PHP sources).
    /// Hub CSS uses /platform-assets so live www stays styled when PHP/epc-static paths 404.
    /// </summary>
    public static readonly IReadOnlyList<string> MarketingStylesheets =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css",
        "/platform-assets/epc_ecomae_platform_marketing.css?v=20260807a",
        "/platform-assets/epc_ecomae_home_sections.css?v=20260811footer",
        "/platform-assets/epc_ecomae_home_3d.css?v=20260807a",
        // LifeOS film band on home (after epm-hub only)
        "/platform-assets/epc_ecomae_marketing_lifeos_film.css?v=20260807b",
        // Layla splash/footer + demo portal — must not rely on HeadContent alone
        "/platform-assets/epc_ecomae_layla_widget.css?v=20260811footer",
        "/platform-assets/epc_ecomae_demo_portal.css?v=20260811footer"
    ];

    /// <summary>Home 3D / scroll helpers used after the marketing hub hero.</summary>
    public static readonly IReadOnlyList<string> MarketingScripts =
    [
        "/platform-assets/epc_ecomae_home_3d.js?v=20260807a"
    ];

    /// <summary>CP OMS (/cp/orders) — PHP epc_orders_cp.css markers via platform-assets.</summary>
    public static readonly IReadOnlyList<string> CpOrdersOmsStylesheets =
    [
        "/platform-assets/epc_orders_cp.css?v=20260811oms",
        "/platform-assets/epc_statuses_cp.css?v=20260811oms"
    ];

    /// <summary>CP Users (/cp/users-app) — PHP user_manager / user.php dual-pane console.</summary>
    public static readonly IReadOnlyList<string> CpUsersConsoleStylesheets =
    [
        "/platform-assets/epc_users_cp.css?v=20260812users"
    ];

    /// <summary>CP Website tracker (PHP epc_web_tracker_cp.css + ASP.NET chart upgrades).</summary>
    public static readonly IReadOnlyList<string> CpWebTrackerStylesheets =
    [
        "/platform-assets/epc_web_tracker_cp.css?v=20260812wt1"
    ];

    /// <summary>Structural selectors / class markers for graphical presentation probes.</summary>
    public static IReadOnlyList<string> RequiredGraphicalMarkers(string surface)
        => surface.Trim().ToLowerInvariant() switch
        {
            "cp" => [".bos-login__bg", "#epcCpParticles", "epc-cp-login-hero", ".bos-login__glow"],
            "erp" => [".bos-login__bg", "epc-erp-portal-bg", "epc-erp-bos-hero", "#erpPortalParticles", ".bos-login__glow"],
            "bos" => [".bos-login__bg", "#bosParticles", ".bos-login__glow"],
            "ip" => [".bos-login__bg", "#ipParticles", ".bos-login__glow", "epc-ip-hub"],
            "lifeos" => [".lifeos-hero", ".lifeos-brand", "lifeos-infographic", "lifeos-flow", "lifeos-ambient"],
            "storefront" => [".epc-engine-animation", ".epc-asp-piston-banner", "epc-home-pro"],
            "marketing" =>
            [
                ".epm-hub", ".epm-hub__orbit-spin", ".epm-hub__matrix", ".epm-hub-section",
                ".epm-lofilm", "#lifeos-film", ".epm-lofilm__video",
                ".epc-demo-portal", ".epc-layla-splash", ".epc-layla-footer-widget"
            ],
            _ => []
        };

    public static string BodyClassFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        // Match cp/templates/bootstrap_admin/desktop.php + epc_cp_shell classes
        "cp" => "fixed-navbar fixed-sidebar epc-cp epc-cp-shell epc-cp-topnav-only epc-cp--blue-theme epc-cp-modern",
        // Match erp_desktop.php standalone shell
        "erp" => "epc-erp-standalone epc-erp-cp-shell epc-cp-shell epc-cp--blue-theme epc-cp-modern",
        // Match bos/index.php
        "bos" => "bos-body bos-body--topnav",
        "ip" => "bos-body bos-body--topnav epc-ip-body",
        "lifeos" => "lifeos-body",
        "storefront" => "epc-storefront-shell",
        "marketing" => "epm-body",
        _ => "epc-migration-shell"
    };

    /// <summary>document.body classes for PHP login/chrome bridges (matches live PHP templates).</summary>
    public static string LoginBodyClassFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        // Same dark BOS login body theme for every tenant CP/ERP login; probe classes retained.
        "cp" => "bos-body bos-body--login blank epc-cp epc-cp-login epc-cp-login-hero epc-cp-shell epc-cp-login--super",
        "erp" => "bos-body bos-body--login epc-erp-standalone epc-erp-cp-shell epc-cp-shell",
        "bos" => "bos-body bos-body--topnav bos-body--login",
        "ip" => "bos-body bos-body--topnav bos-body--login epc-ip-login",
        "lifeos" => "lifeos-body lifeos-body--login",
        "storefront" => "epc-storefront-shell",
        "marketing" => "epm-body",
        _ => BodyClassFor(surfaceKey)
    };

    public static string LegacyChromeSourceFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => "cp/templates/bootstrap_admin/desktop.php",
        "erp" => "cp/templates/bootstrap_admin/erp_desktop.php",
        "bos" => "bos/index.php + bos/epc_bos_shell.css",
        "ip" => "Intelligence Platform /ip (BOS shell + ecosystem hub)",
        "lifeos" => "LifeOS™ UA-AIOS customer product (lifeos.ecomae.com)",
        "storefront" => "templates/nero/desktop.php",
        "marketing" => "content/general_pages/epc_ecomae_platform_layout.php (epm-hub) + epc_ecomae_home_sections.php",
        _ => "unknown"
    };
}
