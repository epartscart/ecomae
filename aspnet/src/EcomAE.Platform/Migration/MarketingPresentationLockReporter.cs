namespace EcomAE.Platform.Migration;

/// <summary>
/// Hard lock: www.ecomae.com marketing presentation (animated hero + all marketing pages) stays PHP.
/// ASP.NET /marketing/app is scaffold-only preview — never a broad <c>location /</c> cutover.
/// </summary>
public sealed class MarketingPresentationLockReporter : IMarketingPresentationLockReporter
{
    public MarketingPresentationLockReport BuildReport()
    {
        return new MarketingPresentationLockReport(
            Status: "php-authoritative",
            LiveHost: "www.ecomae.com",
            LiveHomeUrl: "https://www.ecomae.com/",
            AspNetPreviewRoute: "/marketing/app",
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            RequiredLiveMarkers:
            [
                "epm-hub",
                "epm-hub__orbit-spin",
                "epm-hub__matrix",
                "epm-hub-section",
                "ECOMAE-MARKETING-HOME"
            ],
            ForbiddenLiveMarkers:
            [
                "blazor",
                "ecomae-php-chrome-surface",
                "/marketing/app"
            ],
            AuthoritativePhpSources:
            [
                "index.php → epc_render_ecomae_marketing_home_and_exit()",
                "content/general_pages/epc_ecomae_platform_router.php",
                "content/general_pages/epc_ecomae_platform_layout.php (epc_ecomae_platform_hub)",
                "content/general_pages/epc_ecomae_platform_marketing.css",
                "content/general_pages/epc_ecomae_home_sections.php",
                "content/general_pages/epc_ecomae_platform_pages.php",
                "content/general_pages/epc_ecomae_marketing_pages.php"
            ],
            MarketingPageFloor: Presentation.EcomaeMarketingPages.Count,
            Notes:
            [
                "Live / and marketing routes (/platform/*, /documentation, /compare, /bos, /blockchain, /brochure, /legal, /solutions, …) remain PHP.",
                "ASP.NET /marketing/app reuses PHP epm-hub CSS/markup for dual-sample compare only.",
                "Never invent cutoverAllowed=true or readyForPhpRemoval=true for marketing.",
                "Probe: bash scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh"
            ]);
    }
}

public sealed record MarketingPresentationLockReport(
    string Status,
    string LiveHost,
    string LiveHomeUrl,
    string AspNetPreviewRoute,
    bool CutoverAllowed,
    bool ReadyForPhpRemoval,
    IReadOnlyList<string> RequiredLiveMarkers,
    IReadOnlyList<string> ForbiddenLiveMarkers,
    IReadOnlyList<string> AuthoritativePhpSources,
    int MarketingPageFloor,
    IReadOnlyList<string> Notes);
