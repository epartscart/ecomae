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
            "HONEST: ASP.NET hybrid shells/logins are NOT full PHP presentation or module functionality. Live public often still shows console-wrapped scaffolds. PHP remains authoritative for CP/ERP/BOS/storefront UX. See docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md.",
            surfaces,
            [
                "Blank PhpChromeLayout + Open Sans/PT Sans webfonts + storefront GA4 tag wiring in source (requires redeploy/shadow install to be live).",
                "Unauth /cp|/erp|/bos/app redirect to login pages in source.",
                "Digests/APIs only — not interactive modules (405 CP features, ~160 ERP tabs, ~116 BOS modules still PHP-only).",
                "Probe: bash scripts/cloudpanel_probe_php_presentation_parity.sh (fails until real parity).",
                "PHP remains authoritative until presentation + module function evidence + approval."
            ],
            [
                "FAILING LIVE until redeploy of presentation-match + nginx login shadows + font/analytics parity verified.",
                "Run bash scripts/cloudpanel_probe_php_presentation_parity.sh and attach php-vs-aspnet-recheck.json.",
                "Module inventory: docs/migration/inventory/MODULE_FUNCTION_PARITY_STATUS.md — functional test every php-only row before PHP removal.",
                "Pixel/DOM parity against desktop.php / erp_desktop.php / bos/index.php / modex desktop still required.",
                "Do not enable broad /cp /erp /bos /storefront / cutover and do not remove PHP.",
                "Detailed recheck: docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md"
            ]);
    }
}
