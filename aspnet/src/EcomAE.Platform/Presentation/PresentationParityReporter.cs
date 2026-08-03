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
            "presentation-shell-scaffolded",
            "ASP.NET Core surface shells must reuse PHP chrome CSS/asset URLs and keep JSON digests unchanged for tooling.",
            surfaces,
            [
                "CP/ERP/BOS/storefront shell routes negotiate HTML vs JSON without changing digest JSON schemas.",
                "HTML shells link the same epc-static.php / content/general_pages / templates/modex stylesheets as PHP.",
                "Brand mark uses /content/general_pages/epc_ecomae_logo_svg.php so operator chrome stays ECOM AE-branded.",
                "Unauthorized responses remain JSON 401 so API clients are not surprised by HTML login pages.",
                "PHP remains authoritative for full interactive UX until staging smoke + release-owner approval."
            ],
            [
                "On CloudPanel: ensure→issue→capture authenticated digests before judging HTML chrome parity.",
                "Pixel/DOM parity against live PHP desktop.php / erp_desktop.php / bos/index.php / modex desktop still required before traffic cutover.",
                "Login forms, menu writes, widget interactivity, and storefront cart/checkout HTML are not claimed by this scaffold.",
                "Do not enable broad /cp /erp /bos /storefront cutover until presentation + data parity evidence is attached."
            ]);
    }
}
