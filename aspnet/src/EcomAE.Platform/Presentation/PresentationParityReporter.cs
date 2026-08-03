namespace EcomAE.Platform.Presentation;

public sealed class PresentationParityReporter : IPresentationParityReporter
{
    public PresentationParityReport BuildReport()
    {
        PresentationParitySurface[] surfaces =
        [
            new(
                "cp",
                "/cp/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("cp"),
                LegacyPresentationAssets.StylesheetsFor("cp"),
                "Blazor /cp/app + /cp/login; hybrid nav → PHP modules; ?format=html shell retained",
                "hybrid-chrome-nav-login-bridge"),
            new(
                "erp",
                "/erp/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("erp"),
                LegacyPresentationAssets.StylesheetsFor("erp"),
                "Blazor /erp/app + /erp/login; category nav → PHP ERP areas",
                "hybrid-chrome-nav-login-bridge"),
            new(
                "bos",
                "/bos/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("bos"),
                LegacyPresentationAssets.StylesheetsFor("bos"),
                "Blazor /bos/app + /bos/login; section nav → PHP /BOS/; $_SESSION gap documented",
                "hybrid-chrome-nav-login-bridge"),
            new(
                "storefront",
                "/storefront/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("storefront"),
                LegacyPresentationAssets.StylesheetsFor("storefront"),
                "Blazor /storefront/app + /storefront/login; cart/checkout remain PHP",
                "hybrid-chrome-nav-login-bridge")
        ];

        return new PresentationParityReport(
            "hybrid-chrome-nav-login-bridge",
            "ASP.NET Blazor /cp|/erp|/bos|/storefront {app,login} hybrid shells reuse PHP CSS and link PHP modules; public / /CP/ /ERP/ /BOS/ remain PHP-authoritative. See docs/migration/CHROME_PARITY_GAP_MATRIX.md.",
            surfaces,
            [
                "Blazor presentation apps: /cp/app, /erp/app, /bos/app, /storefront/app with PHP-aligned nav hrefs (LegacyChromeNavCatalog).",
                "Login bridges: /cp/login /erp/login /bos/login /storefront/login + POST /auth/login/admin mint PHP-compatible sessions when EcomAE__SecretSuccession is set.",
                "Apps link the same epc-static.php / content/general_pages / templates/modex / command-dashboard CSS as PHP.",
                "KPI tiles hydrate from ASP.NET digests when an admin cookie is present; unauth still shows chrome layout.",
                "PHP remains authoritative for full interactive UX (menus/widgets/cart/checkout/marketing home, BOS $_SESSION modules) until intentional cutover + approval."
            ],
            [
                "Install previews+logins: ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES bash scripts/cloudpanel_install_presentation_app_shadows.sh",
                "Set EcomAE__SecretSuccession (= PHP secret_succession) in platform.env for login-bridge writes; otherwise UI falls back to PHP login.",
                "Compare side-by-side: PHP /CP/ vs /cp/app; PHP /ERP/ vs /erp/app; PHP /BOS/ vs /bos/app; storefront vs /storefront/app.",
                "Pixel/DOM parity against desktop.php / erp_desktop.php / bos/index.php / modex desktop still required before chrome cutover.",
                "BOS gap: PHP uses $_SESSION; ASP.NET admin cookies unlock digests only — keep /BOS/ for full fleet UX.",
                "Do not enable broad /cp /erp /bos /storefront / cutover and do not remove PHP until presentation + data parity evidence is attached.",
                "Gap matrix: docs/migration/CHROME_PARITY_GAP_MATRIX.md"
            ]);
    }
}
