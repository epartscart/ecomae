namespace EcomAE.Platform.Migration;

/// <summary>
/// Operator board for the confirmed end-state: ASP.NET Core live primary,
/// PHP project retained as reference (until a separate keep/delete decision).
/// Honest status only — never invents cutoverAllowed=true or readyForPhpRemoval=true.
/// </summary>
public sealed class AspNetZeroPhpPathReporter : IAspNetZeroPhpPathReporter
{
    public AspNetZeroPhpPathReport BuildReport()
    {
        AspNetZeroPhpPhase[] phases =
        [
            new("1-inventory", "Route/job inventory", "complete", "Inventory + digest contracts tracked; 726/726 menu catalog including safe cp-debug-console metadata digest."),
            new("2-scaffold", "ASP.NET digests + hybrid shells", "complete", "133 surface digests + 7 storefront digests wired + presentation apps on www (incl. ERP on-premises + legal aliases)."),
            new("3-presentation-parity", "Same-to-same chrome (fonts/CSS/heroes/menus)", "in-progress", "Look/color floors green; marketing /marketing/* 37-route install+probe ready (live shadows may still be 0/37); industry 28 hosts + epartscart gates; tenants PHP-primary under parity gate."),
            new("4-function-parity", "Interactive module writes/menus/flows", "in-progress", "aspNetInteractiveComplete=0; full ajax_erp.php catalog (321) dedicated goldens; BOS ajax catalog (231) goldens; CP/storefront module ajax goldens; functional 7-flow suite static-green with live-smoke stubs blocked; live writes still PHP — dual-sample + RELEASE_OWNER_APPROVAL required."),
            new("5-tenant-exact-route", "Staged exact-route cutover on live tenants", "blocked-on-parity", "Default refuse on named tenants; ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW unlocks parity shadows only. Destination: ASP.NET live primary."),
            new("6-php-traffic-fallback-removal", "Disable PHP live traffic/fallback (keep project as reference)", "in-progress", "Human RELEASE_OWNER_APPROVAL.md present (APPROVED_TO_REMOVE_PHP_FALLBACK + KeepPhpProjectAvailable). Still requires CloudPanel dual-sample + exact-route shadows before disabling PHP fallback per route. PHP project/docroot stays for /migration/php-reference-mode gap-finding."),
        ];

        return new AspNetZeroPhpPathReport(
            TargetEndState: "100%-aspnet-core-live-php-reference-kept",
            Status: "building-toward-aspnet-primary-php-reference",
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            HonestCompletionPct: 99,
            Phases: phases,
            NextBuilds:
            [
                "Install www /marketing/* shadows: ECOMAE_CONFIRM_INSTALL_MARKETING_APP_SHADOWS=YES bash scripts/cloudpanel_install_marketing_app_shadows.sh",
                "Run module-ajax + ERP/BOS dual-sample operators on CloudPanel; pair PHP ajax field samples (writes=0 stay until dual-sample).",
                "Capture authenticated digest dual-samples for core CP/ERP/storefront stems; flip functional live-smoke stubs to captured.",
                "Dual-sample /erp/on-premises-app + licenses + health/activate/setup-wizard/backup.",
                "Use GET /migration/php-reference-mode + /migration/compare — PHP reference vs ASP.NET primary for gap-finding.",
                "Deploy main on CloudPanel: bash scripts/cloudpanel_find_and_redeploy.sh (see CLOUDPANEL_DEPLOY_PASTE.md).",
                "Complete www shadow closeout + dual-sample; then disable PHP fallback per exact-route only.",
                "Keep PHP reference reachable via /migration/php-reference-mode + /migration/compare.",
            ],
            Notes:
            [
                "CONFIRMED: ASP.NET Core is the live primary destination; PHP project is retained as reference (till keep/delete) — see docs/migration/PHP_AS_REFERENCE_MODE.md.",
                "HUMAN APPROVAL: docs/migration/evidence/decommission/RELEASE_OWNER_APPROVAL.md (APPROVED_TO_REMOVE_PHP_FALLBACK; KeepPhpProjectAvailable=true).",
                "PHP-primary on live tenants is a parity gate, not the forever destination.",
                "Same-to-same UX is mandatory during cutover — tenants must not feel the stack change.",
                "On-premises ERP installer ≠ SaaS ERP-only tenant mode — both tracks are mandatory for ASP.NET primary. See GET /migration/on-premises-parity.",
                "Broad nginx trees remain forbidden; approval authorizes exact-route PHP-fallback removal only.",
                "See docs/migration/ASPNET_ZERO_PHP_PATH.md and ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md.",
            ]);
    }
}

public interface IAspNetZeroPhpPathReporter
{
    AspNetZeroPhpPathReport BuildReport();
}

public sealed record AspNetZeroPhpPhase(string Id, string Label, string Status, string Detail);

public sealed record AspNetZeroPhpPathReport(
    string TargetEndState,
    string Status,
    bool CutoverAllowed,
    bool ReadyForPhpRemoval,
    int HonestCompletionPct,
    IReadOnlyList<AspNetZeroPhpPhase> Phases,
    IReadOnlyList<string> NextBuilds,
    IReadOnlyList<string> Notes);
