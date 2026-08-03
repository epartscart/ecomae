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
                "hybrid-chrome-php-login-parity"),
            new(
                "erp",
                "/erp/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("erp"),
                LegacyPresentationAssets.StylesheetsFor("erp"),
                "Blazor /erp/app + /erp/login; category nav → PHP ERP areas",
                "hybrid-chrome-php-login-parity"),
            new(
                "bos",
                "/bos/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("bos"),
                LegacyPresentationAssets.StylesheetsFor("bos"),
                "Blazor /bos/app + /bos/login; section nav → PHP /BOS/; $_SESSION gap documented",
                "hybrid-chrome-php-login-parity"),
            new(
                "storefront",
                "/storefront/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("storefront"),
                LegacyPresentationAssets.StylesheetsFor("storefront"),
                "Blazor /storefront/app + /storefront/login; cart/checkout remain PHP",
                "hybrid-chrome-php-login-parity")
        ];

        return new PresentationParityReport(
            "hybrid-chrome-php-login-parity",
            "ASP.NET Blazor /cp|/erp|/bos|/storefront {app,login} use blank PhpChromeLayout (no migration-console chrome). Unauth /…/app redirects to PHP-matching login landings; authenticated apps keep hybrid PHP-linked nav. Public / /CP/ /ERP/ /BOS/ remain PHP-authoritative.",
            surfaces,
            [
                "Blank PhpChromeLayout for product previews/logins — MigrationConsoleLayout only on /migration/console.",
                "Unauthenticated /cp/app|/erp/app|/bos/app redirect to matching login pages (same as PHP gate).",
                "Login landings mirror PHP: CP centered blue BOS login, ERP dark marketing+sign-in, BOS split operator panel, storefront customer card.",
                "Authenticated apps: PHP-aligned nav hrefs (LegacyChromeNavCatalog) + digest KPIs.",
                "PHP remains authoritative for full interactive UX until intentional cutover + approval."
            ],
            [
                "Install previews+logins: ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES bash scripts/cloudpanel_install_presentation_app_shadows.sh",
                "Set EcomAE__SecretSuccession (= PHP secret_succession) in platform.env for login-bridge writes; otherwise UI falls back to PHP login.",
                "Side-by-side after redeploy: PHP /CP/ vs /cp/login; /ERP/ vs /erp/login; /BOS/ vs /bos/login; epartscart.com vs /storefront/app.",
                "Pixel/DOM parity against desktop.php / erp_desktop.php / bos/index.php / modex desktop still required before chrome cutover.",
                "BOS gap: PHP uses $_SESSION; ASP.NET admin cookies unlock digests only — keep /BOS/ for full fleet UX.",
                "Do not enable broad /cp /erp /bos /storefront / cutover and do not remove PHP.",
                "Gap matrix: docs/migration/CHROME_PARITY_GAP_MATRIX.md"
            ]);
    }
}
