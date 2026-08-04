namespace EcomAE.Platform.Migration;

/// <summary>
/// Operator board for the PHP on-premises ERP product track.
/// Distinct from SaaS ERP-only tenant mode (<c>TenantMode.ErpOnlyTenant</c>) — installer + license + health stay PHP until dual-sample.
/// Never invents cutoverAllowed=true or readyForPhpRemoval=true.
/// </summary>
public interface IOnPremisesParityReporter
{
    OnPremisesParityReport BuildReport();
}

public sealed class OnPremisesParityReporter : IOnPremisesParityReporter
{
    public OnPremisesParityReport BuildReport()
    {
        OnPremisesParityTrack[] tracks =
        [
            new(
                "erp-only-tenant",
                "SaaS ERP-only tenant mode",
                "in-progress",
                "TenantMode.ErpOnlyTenant + erp_only_shared mapping already in ASP.NET. Dual-sample client-erp navigation still PHP-primary."),
            new(
                "on-prem-installer",
                "Self-hosted installer pack",
                "php-authoritative",
                "deploy/on-premises/* (setup-wizard, activate-license, license_manager, health-check, backup, docker-compose) remains PHP until ASP.NET pack + dual-sample."),
            new(
                "on-prem-license-api",
                "License activate + health intake APIs",
                "php-authoritative",
                "api/v1/licenses/activate.php + api/v1/on-premises/health.php + epc_onprem_licenses registry remain PHP. ASP.NET health + activate dry-runs only (writes=0)."),
            new(
                "erp-on-premises-tab",
                "ERP operator On-Premises tab",
                "scaffold",
                "PHP erp_tabs_on_premises.php authoritative. ASP.NET /erp/on-premises-app overview scaffold only until dual-sample."),
        ];

        return new OnPremisesParityReport(
            Role: "on-premises-erp-parity",
            Status: "building-toward-zero-php",
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            PhpAuthoritative: true,
            AspNetRouteHint: "/erp/on-premises-app",
            PhpTab: "/ERP/?epc_erp_shell=1&tab=on_premises",
            Tracks: tracks,
            PhpPaths:
            [
                "cp/content/shop/finance/erp/erp_tabs_on_premises.php",
                "deploy/on-premises/setup-wizard.php",
                "deploy/on-premises/activate-license.php",
                "deploy/on-premises/epc_license_manager.php",
                "deploy/on-premises/health-check.php",
                "deploy/on-premises/backup.php",
                "api/v1/on-premises/health.php",
                "api/v1/licenses/activate.php",
                "content/general_pages/epc_onprem_licenses.php",
                "epc-onprem-license-generate.php"
            ],
            NextBuilds:
            [
                "Dual-sample /erp/on-premises-app vs PHP on_premises tab.",
                "Read digest over epc_onprem_licenses (omit secrets/notes) after schema dual-sample.",
                "Promote health + license-activate dry-runs via write-dryrun operator; PHP remains authoritative.",
                "ASP.NET Core on-prem installer pack (replace PHP runtime in deploy/on-premises) — separate from SaaS ERP-only tenants.",
            ],
            Notes:
            [
                "ERP-only SaaS tenant mode ≠ on-premises installer product — both must reach 0 PHP.",
                "Never invent RELEASE_OWNER_APPROVAL.md. cutoverAllowed stays false until dual-sample + human sign-off.",
                "See docs/migration/ASPNET_ZERO_PHP_PATH.md § On-premises ERP.",
            ]);
    }
}

public sealed record OnPremisesParityTrack(string Id, string Label, string Status, string Detail);

public sealed record OnPremisesParityReport(
    string Role,
    string Status,
    bool CutoverAllowed,
    bool ReadyForPhpRemoval,
    bool PhpAuthoritative,
    string AspNetRouteHint,
    string PhpTab,
    IReadOnlyList<OnPremisesParityTrack> Tracks,
    IReadOnlyList<string> PhpPaths,
    IReadOnlyList<string> NextBuilds,
    IReadOnlyList<string> Notes);
