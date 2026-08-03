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
                "scaffold-not-full-php-parity"),
            new(
                "erp",
                "/erp/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("erp"),
                LegacyPresentationAssets.StylesheetsFor("erp"),
                "Blazor /erp/app + /erp/login; category nav → PHP ERP areas",
                "scaffold-not-full-php-parity"),
            new(
                "bos",
                "/bos/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("bos"),
                LegacyPresentationAssets.StylesheetsFor("bos"),
                "Blazor /bos/app + /bos/login; section nav → PHP /BOS/; $_SESSION gap documented",
                "scaffold-not-full-php-parity"),
            new(
                "storefront",
                "/storefront/app",
                LegacyPresentationAssets.LegacyChromeSourceFor("storefront"),
                LegacyPresentationAssets.StylesheetsFor("storefront"),
                "Blazor /storefront/app + /storefront/login; cart/checkout remain PHP",
                "scaffold-not-full-php-parity")
        ];

        return new PresentationParityReport(
            "scaffold-not-full-php-parity",
            "HONEST: Batch 1 puts PHP webfonts/CSS/analytics into <head> via PhpSurfaceHead + hybrid login enrichments. Full desktop pixel parity and interactive modules remain incomplete. PHP remains authoritative. See docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md.",
            surfaces,
            [
                "PhpSurfaceHead (HeadOutlet) injects Open Sans / PT Sans / Fraunces+Sora / Inter+JetBrains + surface stylesheets + storefront GA4.",
                "Login pages reuse epc_cp_login(_hero) CSS and catalogue PHP module cards/deeplinks.",
                "Hybrid module directories on /cp|/erp|/bos|/storefront/app (Batch 0).",
                "Probe: bash scripts/cloudpanel_probe_php_presentation_parity.sh — chrome-pass possible while functionality still pending.",
                "PHP remains authoritative until presentation + module function evidence + approval."
            ],
            [
                "Redeploy main + ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES bash scripts/cloudpanel_install_presentation_app_shadows.sh",
                "Run bash scripts/cloudpanel_probe_php_presentation_parity.sh and commit php-vs-aspnet-recheck.json.",
                "Batch 2: deepen authenticated chrome toward desktop.php / erp_desktop.php / bos/index.php / modex.",
                "Module inventory: docs/migration/inventory/MODULE_FUNCTION_PARITY_STATUS.md",
                "Do not enable broad /cp /erp /bos /storefront / cutover and do not remove PHP.",
                "Detailed recheck: docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md"
            ]);
    }
}
