namespace EcomAE.Platform.Migration;

/// <summary>
/// Marketing presentation parity gate for www.ecomae.com.
/// Target end-state: ASP.NET serves marketing (including animated epm-hub); PHP removed.
/// Until dual-sample same-to-same, live / stays PHP — not a permanent ban.
/// </summary>
public sealed class MarketingPresentationLockReporter : IMarketingPresentationLockReporter
{
    public MarketingPresentationLockReport BuildReport()
    {
        return new MarketingPresentationLockReport(
            Status: "parity-gate-php-primary-until-aspnet-same-to-same",
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
                "blazor",
                "ecomae-php-chrome-surface"
            ],
            AuthoritativePhpSourcesUntilCutover:
            [
                "index.php → epc_render_ecomae_marketing_home_and_exit()",
                "content/general_pages/epc_ecomae_platform_router.php",
                "content/general_pages/epc_ecomae_platform_layout.php (epc_ecomae_platform_hub)",
                "content/general_pages/epc_ecomae_platform_marketing.css",
                "content/general_pages/epc_ecomae_home_sections.php",
                "content/general_pages/epc_ecomae_platform_pages.php",
                "content/general_pages/epc_ecomae_marketing_pages.php"
            ],
            UnlockCriteria:
            [
                "ASP.NET /marketing/app dual-sample matches live epm-hub + home sections pixel/structure.",
                "All marketing routes catalogued in EcomaeMarketingPages have ASP.NET or hybrid parity.",
                "Exact-route promotion of / and marketing paths (never invent broad location / without approval).",
                "Human RELEASE_OWNER_APPROVAL.md — never invent this file.",
            ],
            MarketingPageFloor: Presentation.EcomaeMarketingPages.Count,
            Notes:
            [
                "TARGET: 100% ASP.NET Core / 0 PHP for ecomae.com marketing.",
                "Live / is PHP-primary only until ASP.NET same-to-same — parity gate, not destination.",
                "ASP.NET /marketing/app now includes epm-hub + #ehm-home-sections (PhpEcomaeHomeSections) as the replacement scaffold.",
                "cutoverAllowed=false until dual-sample + approval; never invent true.",
                "Probe: bash scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh",
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
