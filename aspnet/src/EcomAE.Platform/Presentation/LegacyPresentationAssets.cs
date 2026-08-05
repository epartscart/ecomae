namespace EcomAE.Platform.Presentation;

/// <summary>
/// Asset URLs that match the live PHP chrome so ASP.NET Core shells reuse the same CSS/JS presentation.
/// Paths intentionally point at the existing PHP/static pipeline (epc-static.php + content/general_pages CSS helpers).
/// </summary>
public static class LegacyPresentationAssets
{
    public const string BrandName = "ECOM AE";
    public const string BrandMarkUrl = "/content/general_pages/epc_ecomae_logo_svg.php";

    public static IReadOnlyList<string> StylesheetsFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => ControlPanelStylesheets,
        "erp" => ErpStylesheets,
        "bos" => BosStylesheets,
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
        "/content/general_pages/epc_cp_command_dashboard_css.php"
    ];

    /// <summary>
    /// Library entry (requires _ASTEXE_) — do not use as &lt;img src&gt;.
    /// Prefer <see cref="AnimatedEpartsCartFragmentUrl"/> for HTML/SVG embed.
    /// </summary>
    public const string EpartsCartMarkUrl = "/content/general_pages/epc_animated_epartscart_logo.php";

    /// <summary>Renderable animated cart logo fragment (HTML/SVG), matches PHP storefront/CP embeds.</summary>
    public const string AnimatedEpartsCartFragmentUrl = "/content/general_pages/animated_epartscart_logo.php";

    /// <summary>PHP CP login hero/panel CSS reused by ASP.NET /cp/login.</summary>
    public static readonly IReadOnlyList<string> LoginStylesheets =
    [
        "/content/general_pages/epc_cp_login_css.php",
        "/content/general_pages/epc_cp_login_hero_css.php",
        "/content/general_pages/epc_ecomae_hub_logo_css.php"
    ];

    /// <summary>Standalone ERP portal login shell (bos-hero, particles, glass panel).</summary>
    public static readonly IReadOnlyList<string> ErpLoginStylesheets =
    [
        "/content/shop/finance/epc_erp_portal_inline_css_serve.php"
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
        "/content/general_pages/epc_cp_professional_css.php"
    ];

    public static readonly IReadOnlyList<string> BosStylesheets =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css",
        "/epc-static.php?f=bos/epc_bos_shell.css"
    ];

    public static readonly IReadOnlyList<string> StorefrontStylesheets =
    [
        // epartscart.com live theme is nero (templates/nero/desktop.php), not modex.
        "/epc-static.php?f=templates/nero/assets/css/style_all.css",
        "/epc-static.php?f=templates/nero/css/astself.css",
        "/epc-static.php?f=templates/nero/css/catalogue/catalogue.css",
        "/epc-static.php?f=templates/nero/css/docpart/style.css",
        "/modules/slider/css/style.css",
        // Animated eparts cart logo (PHP enqueue equivalent)
        "/aspnet-php-assets/eparts-animated-logo.css",
        // PHP site_professional_shell.php polish (red logo, pill CTAs, dark search bar, navy tiles)
        "/content/general_pages/epc_storefront_professional_shell_css.php",
        // Container width (98% / 1728px) + piston hero (requires html data-epc-industry/storefront attrs)
        "/content/general_pages/epc_automotive_spareparts.css"
    ];

    /// <summary>
    /// www.ecomae.com marketing chrome — animated epm-hub hero + home sections (PHP sources).
    /// </summary>
    public static readonly IReadOnlyList<string> MarketingStylesheets =
    [
        "https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css",
        "/content/general_pages/epc_ecomae_platform_marketing_css.php",
        "/epc-static.php?f=content/general_pages/epc_ecomae_home_sections.css&v=20260716d",
        "/epc-static.php?f=content/general_pages/epc_ecomae_home_3d.css&v=20260716d"
    ];

    /// <summary>Home 3D / scroll helpers used after the marketing hub hero.</summary>
    public static readonly IReadOnlyList<string> MarketingScripts =
    [
        "/epc-static.php?f=content/general_pages/epc_ecomae_home_3d.js&v=20260716d"
    ];

    /// <summary>Structural selectors / class markers for graphical presentation probes.</summary>
    public static IReadOnlyList<string> RequiredGraphicalMarkers(string surface)
        => surface.Trim().ToLowerInvariant() switch
        {
            "cp" => [".ech-hub", "#epcCpParticles", "epc-cp-login-hero"],
            "erp" => [".ech-hub", "epc-erp-portal-bg", "epc-erp-bos-hero", "#erpPortalParticles"],
            "bos" => [".bos-login__bg", "#bosParticles", ".bos-login__glow"],
            "storefront" => [".epc-engine-animation", ".epc-asp-piston-banner", "epc-home-pro"],
            "marketing" =>
            [
                ".epm-hub", ".epm-hub__orbit-spin", ".epm-hub__matrix", ".epm-hub-section",
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
        "storefront" => "epc-storefront-shell",
        "marketing" => "epm-body",
        _ => "epc-migration-shell"
    };

    /// <summary>document.body classes for PHP login/chrome bridges (matches live PHP templates).</summary>
    public static string LoginBodyClassFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => "blank epc-cp epc-cp-login epc-cp-login-hero epc-cp-shell epc-cp-login--super epc-cp--blue-theme epc-cp-modern",
        "erp" => "epc-erp-standalone epc-erp-cp-shell epc-cp-shell",
        "bos" => "bos-body bos-body--topnav bos-body--login",
        "storefront" => "epc-storefront-shell",
        "marketing" => "epm-body",
        _ => BodyClassFor(surfaceKey)
    };

    public static string LegacyChromeSourceFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => "cp/templates/bootstrap_admin/desktop.php",
        "erp" => "cp/templates/bootstrap_admin/erp_desktop.php",
        "bos" => "bos/index.php + bos/epc_bos_shell.css",
        "storefront" => "templates/nero/desktop.php",
        "marketing" => "content/general_pages/epc_ecomae_platform_layout.php (epm-hub) + epc_ecomae_home_sections.php",
        _ => "unknown"
    };
}
