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
            new("2-scaffold", "ASP.NET digests + hybrid shells", "complete", "128 surface digests + storefront digests + ~157 presentation apps on www."),
            new("3-presentation-parity", "Same-to-same chrome (fonts/CSS/heroes/menus)", "in-progress", "Marketing nav+solutions scaffolds include free-tools/platform-guides; CP/ERP/BOS/storefront hybrid on www; tenants PHP-primary under parity gate."),
            new("4-function-parity", "Interactive module writes/menus/flows", "in-progress", "aspNetInteractiveComplete=0; cart/quote/garage + OMS + ERP cash/GL/purchase/invoice/SO dry-runs; live writes still PHP."),
            new("5-tenant-exact-route", "Staged exact-route cutover on live tenants", "blocked-on-parity", "Default refuse on named tenants; ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW unlocks parity shadows only."),
            new("6-php-removal", "Disable PHP + remove runtime", "blocked", "Requires dual-sample + human RELEASE_OWNER_APPROVAL.md — never invent that file."),
        ];

        return new AspNetZeroPhpPathReport(
            TargetEndState: "100%-aspnet-core-0-php",
            Status: "building-toward-zero-php",
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            HonestCompletionPct: 53,
            Phases: phases,
            NextBuilds:
            [
                "Dual-sample marketing scaffolds vs live PHP → exact-route candidates.",
                "Dual-sample cart/quote/garage write dry-runs vs PHP ajax, then promote.",
                "Dual-sample OMS + ERP void/cancel/reverse dry-runs vs PHP ajax, then promote.",
                "Customer-results / API docs scaffolds; per-tenant storefront theme parity (epartscart first).",
                "Capture dual-sample evidence per surface; only then staged exact-route on tenants.",
            ],
            Notes:
            [
                "PHP-primary on live tenants is a parity gate, not the destination.",
                "Same-to-same UX is mandatory during cutover — tenants must not feel the stack change.",
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
