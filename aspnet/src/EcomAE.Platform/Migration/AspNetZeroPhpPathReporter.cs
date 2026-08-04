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
            new("1-inventory", "Route/job inventory", "complete", "Inventory + digest contracts tracked; cp-debug-console holdout intentional."),
            new("2-scaffold", "ASP.NET digests + hybrid shells", "complete", "128 surface digests + storefront digests + ~184 presentation apps on www (incl. ERP on-premises + legal aliases)."),
            new("3-presentation-parity", "Same-to-same chrome (fonts/CSS/heroes/menus)", "in-progress", "Marketing solutions+resources+full legal alias set+brochure-cp scaffolded; CP/ERP/BOS/storefront hybrid on www; ERP on-premises overview scaffolded; tenants PHP-primary under parity gate."),
            new("4-function-parity", "Interactive module writes/menus/flows", "in-progress", "aspNetInteractiveComplete=0; full ajax_erp.php catalog (321) all dedicated; BOS ajax_epc_bos catalog; CP/storefront module ajax+classic form catalog (394 actions) + POS/portal/on-prem pack dry-runs; CP integrations field parity (payments/carriers/Amazon/hub); on-premises scaffolds; write-dryrun dual-sample operator floor; live writes still PHP — dual-sample + RELEASE_OWNER_APPROVAL still required for 100%."),
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
                "Run module-ajax dual-sample operator on CloudPanel; pair PHP ajax field samples; keep cutoverAllowed=false.",
                "Dual-sample ERP ajax registry + dedicated BOS/concurrency/OPL dry-runs vs PHP ajax_erp.php.",
                "Dual-sample /erp/on-premises-app + licenses + health/activate/setup-wizard/backup; grow on-premises-aspnet pack.",
                "Paired PHP ajax vs ASP.NET write dry-run samples via write-dryrun + module-ajax operators; then staged exact-route.",
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
