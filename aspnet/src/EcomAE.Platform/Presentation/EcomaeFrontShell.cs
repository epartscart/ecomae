namespace EcomAE.Platform.Presentation;

/// <summary>
/// Front template desktops and core helpers. Chrome is
/// <c>PhpStorefrontDesktopChrome</c>; module/page dispatch stays on the
/// existing storefront apps.
/// </summary>
public static class EcomaeFrontShell
{
    public static readonly string[] Paths =
    [
        "core/dp_content.php",
        "core/dp_core.php",
        "core/dp_helper.php",
        "core/dp_module.php",
        "core/dp_template.php",
        "templates/expan/desktop.php",
        "templates/limo/desktop.php",
        "templates/modex/desktop.php",
    ];
}
