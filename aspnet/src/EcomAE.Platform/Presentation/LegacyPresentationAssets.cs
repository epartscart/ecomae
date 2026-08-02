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
        _ => ControlPanelStylesheets
    };

    public static readonly IReadOnlyList<string> ControlPanelStylesheets =
    [
        "/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fontawesome/css/font-awesome.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/css/bootstrap.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/fonts/pe-icon-7-stroke/css/pe-icon-7-stroke.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/css/astself.css",
        "/content/general_pages/epc_cp_ui_css.php",
        "/content/general_pages/epc_cp_professional_css.php"
    ];

    public static readonly IReadOnlyList<string> ErpStylesheets =
    [
        "/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fontawesome/css/font-awesome.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/css/bootstrap.css",
        "/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css",
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
        "/epc-static.php?f=templates/modex/assets/css/preload.css",
        "/epc-static.php?f=templates/modex/assets/css/style_color.css",
        "/epc-static.php?f=templates/modex/assets/css/width-boxed.css",
        "/epc-static.php?f=templates/modex/css/catalogue/catalogue.css",
        "/epc-static.php?f=templates/modex/css/astself.css",
        "/epc-static.php?f=templates/modex/css/docpart/style.css"
    ];

    public static string BodyClassFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => "epc-cp-shell fixed-navbar fixed-sidebar",
        "erp" => "epc-erp-shell fixed-navbar fixed-sidebar",
        "bos" => "epc-bos-shell",
        "storefront" => "epc-storefront-shell",
        _ => "epc-migration-shell"
    };

    public static string LegacyChromeSourceFor(string surfaceKey) => surfaceKey.Trim().ToLowerInvariant() switch
    {
        "cp" => "cp/templates/bootstrap_admin/desktop.php",
        "erp" => "cp/templates/bootstrap_admin/erp_desktop.php",
        "bos" => "bos/index.php + bos/epc_bos_shell.css",
        "storefront" => "templates/modex/desktop.php",
        _ => "unknown"
    };
}
