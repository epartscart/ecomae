namespace EcomAE.Platform.Migration;

/// <summary>
/// Marketing presentation parity gate for www.ecomae.com.
/// Product intent: ASP.NET serves marketing (including animated epm-hub); PHP under /php-reference only.
/// cutoverAllowed stays false until dual-sample + RELEASE_OWNER_APPROVAL.
/// </summary>
public sealed class MarketingPresentationLockReporter : IMarketingPresentationLockReporter
{
    public MarketingPresentationLockReport BuildReport()
    {
        return new MarketingPresentationLockReport(
            Status: "aspnet-primary-parity-gate-php-reference-kept",
            LiveHost: "www.ecomae.com",
            LiveHomeUrl: "https://www.ecomae.com/",
            AspNetPreviewRoute: "/marketing/app",
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            TargetEndState: "100%-aspnet-core-live-php-reference-kept",
            RequiredLiveMarkers:
            [
                "epm-hub",
                "epm-hub__orbit-spin",
                "epm-hub__matrix",
                "epm-hub-section",
                "ECOMAE-MARKETING-HOME"
            ],
            ForbiddenLiveMarkersUntilCutover:
            [
                // Warmup splash / stuck classic-entry — not acceptable as product home.
                "Loading — Please wait",
                "x-ecomae-php-serving"
            ],
            AuthoritativePhpSourcesUntilCutover:
            [
                "index.php → epc_render_ecomae_marketing_home_and_exit() (php-reference only)",
                "content/general_pages/epc_ecomae_platform_router.php",
                "content/general_pages/epc_ecomae_platform_layout.php (epc_ecomae_platform_hub)",
                "content/general_pages/epc_ecomae_platform_marketing.css",
                "content/general_pages/epc_ecomae_home_sections.php",
                "content/general_pages/epc_ecomae_platform_pages.php",
                "content/general_pages/epc_ecomae_marketing_pages.php"
            ],
            UnlockCriteria:
            [
                "Classic-entry proxies www / → ASP.NET /marketing/app with epm-hub markers live.",
                "All marketing routes catalogued in EcomaeMarketingPages have ASP.NET or hybrid parity.",
                "FORCE_LIVE_ALL_SITES / FORCE_LIVE_WWW_MARKETING republishes :5100 after merge.",
                "Human RELEASE_OWNER_APPROVAL.md for PHP traffic/fallback removal — never invent this file.",
            ],
            MarketingPageFloor: Presentation.EcomaeMarketingPages.Count,
            Notes:
            [
                "TARGET: 100% ASP.NET Core / 0 PHP product for ecomae.com marketing (PHP reference kept).",
                "Product stackToday=aspnet via classic-entry; PHP compare under /php-reference/home.",
                "ASP.NET /marketing/app includes epm-hub + #ehm-home-sections (PhpEcomaeHomeSections).",
                "cutoverAllowed=false until dual-sample + approval; never invent true.",
                "Probe: bash scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh (or FORCE_LIVE_ALL_SITES).",
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
    string TargetEndState,
    IReadOnlyList<string> RequiredLiveMarkers,
    IReadOnlyList<string> ForbiddenLiveMarkersUntilCutover,
    IReadOnlyList<string> AuthoritativePhpSourcesUntilCutover,
    IReadOnlyList<string> UnlockCriteria,
    int MarketingPageFloor,
    IReadOnlyList<string> Notes);
