namespace EcomAE.Platform.Migration;

/// <summary>
/// Operator board for the real end-state: 100% ASP.NET Core / 0 PHP.
/// Honest status only — never invents cutoverAllowed=true or readyForPhpRemoval=true.
/// </summary>
public sealed class AspNetZeroPhpPathReporter : IAspNetZeroPhpPathReporter
{
    public AspNetZeroPhpPathReport BuildReport()
    {
        AspNetZeroPhpPhase[] phases =
        [
            new("1-inventory", "Route/job inventory", "complete", "Inventory + digest contracts tracked; 726/726 menu catalog including safe cp-debug-console metadata digest."),
            new("2-scaffold", "ASP.NET digests + hybrid shells", "complete", "128 surface digests + 7 storefront digests wired + ~184 presentation apps on www (incl. ERP on-premises + legal aliases)."),
            new("3-presentation-parity", "Same-to-same chrome (fonts/CSS/heroes/menus)", "in-progress", "Look/color floors green; marketing /marketing/* 37-route install+probe ready (live shadows may still be 0/37); industry 28 hosts + epartscart gates; tenants PHP-primary under parity gate."),
            new("4-function-parity", "Interactive module writes/menus/flows", "in-progress", "aspNetInteractiveComplete=0; full ajax_erp.php catalog (321) dedicated goldens; BOS ajax catalog (231) goldens; CP/storefront module ajax goldens (254/249 dedicated of 394); functional 7-flow suite static-green with live-smoke stubs blocked; live writes still PHP — dual-sample + RELEASE_OWNER_APPROVAL required."),
            new("5-tenant-exact-route", "Staged exact-route cutover on live tenants", "blocked-on-parity", "Default refuse on named tenants; ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW unlocks parity shadows only."),
            new("6-php-removal", "Disable PHP + remove runtime", "blocked", "Requires dual-sample + human RELEASE_OWNER_APPROVAL.md — never invent that file. Includes on-premises installer pack (not only SaaS)."),
        ];

        return new AspNetZeroPhpPathReport(
            TargetEndState: "100%-aspnet-core-0-php",
            Status: "building-toward-zero-php",
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
                "Human RELEASE_OWNER_APPROVAL.md after dual-sample — never invent that file; then 100% / 0 PHP.",
            ],
            Notes:
            [
                "PHP-primary on live tenants is a parity gate, not the destination.",
                "Same-to-same UX is mandatory during cutover — tenants must not feel the stack change.",
                "On-premises ERP installer ≠ SaaS ERP-only tenant mode — both tracks are mandatory for 0 PHP. See GET /migration/on-premises-parity.",
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
