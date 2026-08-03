namespace EcomAE.Platform.Presentation;

public sealed class PresentationParityReporter : IPresentationParityReporter
{
    public PresentationParityReport BuildReport()
    {
        PresentationParitySurface[] surfaces =
        [
            new(
                "cp",
                "/cp?format=html",
                LegacyPresentationAssets.LegacyChromeSourceFor("cp"),
                LegacyPresentationAssets.StylesheetsFor("cp"),
                "default json; Accept: text/html or ?format=html",
                "presentation-shell-scaffolded"),
            new(
                "erp",
                "/erp?format=html",
                LegacyPresentationAssets.LegacyChromeSourceFor("erp"),
                LegacyPresentationAssets.StylesheetsFor("erp"),
                "default json; Accept: text/html or ?format=html",
                "presentation-shell-scaffolded"),
            new(
                "bos",
                "/bos?format=html",
                LegacyPresentationAssets.LegacyChromeSourceFor("bos"),
                LegacyPresentationAssets.StylesheetsFor("bos"),
                "default json; Accept: text/html or ?format=html",
                "presentation-shell-scaffolded"),
            new(
                "storefront",
                "/storefront/account?format=html",
                LegacyPresentationAssets.LegacyChromeSourceFor("storefront"),
                LegacyPresentationAssets.StylesheetsFor("storefront"),
                "default json; Accept: text/html or ?format=html",
                "presentation-shell-scaffolded")
        ];

        return new PresentationParityReport(
            "presentation-app-preview-scaffolded",
            "ASP.NET Blazor /cp/app /erp/app /bos/app /storefront/app previews reuse PHP chrome CSS; public / /CP/ /ERP/ /BOS/ remain PHP-authoritative.",
            surfaces,
            [
                "Blazor presentation apps: /cp/app (Control Command Centre), /erp/app (Ecom BOS), /bos/app (fleet), /storefront/app (storefront preview).",
                "Apps link the same epc-static.php / content/general_pages / templates/modex / command-dashboard CSS as PHP.",
                "KPI tiles hydrate from ASP.NET digests when an admin cookie is present; unauth still shows chrome layout.",
                "Legacy ?format=html shells remain for /cp /erp /bos aliases (admin session).",
                "PHP remains authoritative for full interactive UX (login, menus, cart/checkout, marketing home) until intentional cutover + approval."
            ],
            [
                "Install previews: ECOMAE_CONFIRM_INSTALL_PRESENTATION_APP_SHADOWS=YES bash scripts/cloudpanel_install_presentation_app_shadows.sh",
                "Compare side-by-side: PHP /CP/ vs ASP.NET /cp/app; PHP /ERP/ vs /erp/app; PHP storefront vs /storefront/app.",
                "Pixel/DOM parity against desktop.php / erp_desktop.php / bos/index.php / modex desktop + ecomae marketing layout still required before chrome cutover.",
                "Do not enable broad /cp /erp /bos /storefront / cutover and do not remove PHP until presentation + data parity evidence is attached."
            ]);
    }
}
