using EcomAE.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Declares the confirmed dual-runtime model: ASP.NET primary intent + PHP kept as reference.
/// Never invents cutoverAllowed / readyForPhpRemoval / RELEASE_OWNER_APPROVAL.
/// </summary>
public sealed class PhpReferenceModeReporter : IPhpReferenceModeReporter
{
    private readonly PhpReferenceOptions _reference;
    private readonly MigrationRouteCutoverOptions _cutover;

    public PhpReferenceModeReporter(
        IOptions<PhpReferenceOptions> reference,
        IOptions<MigrationRouteCutoverOptions> cutover)
    {
        _reference = reference.Value;
        _cutover = cutover.Value;
    }

    public PhpReferenceModeReport BuildReport()
    {
        var wwwPhp = TrimBase(_reference.WwwPhpBaseUrl);
        var tenantPhp = TrimBase(_reference.TenantPhpBaseUrl);
        var dedicatedCp = TrimBase(_reference.DedicatedCpPhpBaseUrl);
        var aspNet = TrimBase(_reference.AspNetPrimaryBaseUrl);

        // www classic entries (/ /cp/ /erp/ /bos/) redirect to ASP.NET apps after human-confirmed
        // classic-entry install. PHP reference uses /index.php + deep module paths that still hit PHP.
        var pairs = new List<PhpReferenceComparePair>
        {
            new("marketing", $"{wwwPhp}/index.php", $"{aspNet}/marketing/app", "php-reference-vs-aspnet-primary"),
            new("cp", $"{wwwPhp}/cp/shop/orders/orders", $"{aspNet}/cp/app", "php-reference-deep-vs-aspnet-primary"),
            new("erp", $"{wwwPhp}/erp/", $"{aspNet}/erp/app", "php-reference-entry-or-deep-vs-aspnet-primary"),
            new("bos", $"{wwwPhp}/bos/", $"{aspNet}/bos/app", "php-reference-entry-or-deep-vs-aspnet-primary"),
            new("cp-dedicated", $"{dedicatedCp}/CP/", $"{aspNet}/cp/app", "php-reference-vs-aspnet-preview"),
            new("tenant-storefront", $"{tenantPhp}/", $"{aspNet}/storefront/app", "tenant-php-reference-www-aspnet-preview-only"),
            new("tenant-cp", $"{tenantPhp}/CP/", $"{aspNet}/cp/app", "tenant-php-reference-www-aspnet-preview-only"),
            new("tenant-erp", $"{tenantPhp}/ERP/", $"{aspNet}/erp/app", "tenant-php-reference-www-aspnet-preview-only"),
        };

        var status = !_reference.Enabled
            ? "php-reference-disabled"
            : _reference.KeepPhpProjectAvailable
                ? "aspnet-primary-intent-php-reference-retained"
                : "php-reference-misconfigured-keep-project-false";

        return new PhpReferenceModeReport(
            Status: status,
            Mode: string.IsNullOrWhiteSpace(_reference.Mode) ? "aspnet-primary-php-reference" : _reference.Mode,
            Enabled: _reference.Enabled,
            ArchitectureConfirmed: _reference.ArchitectureConfirmed,
            KeepPhpProjectAvailable: _reference.KeepPhpProjectAvailable,
            // Hard locks — never invent green cutover from this board.
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            RequirePhpFallback: _cutover.RequirePhpFallback,
            StorefrontAspNetEnabled: _cutover.StorefrontAspNetEnabled,
            AdminAspNetEnabled: _cutover.AdminAspNetEnabled,
            WwwPhpBaseUrl: wwwPhp,
            TenantPhpBaseUrl: tenantPhp,
            DedicatedCpPhpBaseUrl: dedicatedCp,
            AspNetPrimaryBaseUrl: aspNet,
            PhpDocRoot: string.IsNullOrWhiteSpace(_reference.PhpDocRoot) ? null : _reference.PhpDocRoot.Trim(),
            ComparePairs: pairs,
            OperatorSteps:
            [
                "Classic PHP entries → ASP.NET: ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh",
                "Full operator: ECOMAE_CONFIRM_ASPNET_PRIMARY_CUTOVER=YES bash scripts/cloudpanel_execute_aspnet_primary_cutover_operator.sh",
                "PHP reference URLs: /index.php (home) + deep /cp|/erp|/bos module paths; compare at /migration/compare",
                "Run dual-sample compare_* scripts against PHP reference URLs while ASP.NET serves primary entries.",
                "Do not delete PHP source until a separate decommission gate (ReadyToRemovePhp) — reference mode is not deletion.",
                "Rollback live traffic with: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback"
            ],
            HardLocks:
            [
                "cutoverAllowed=false (this reporter always — traffic still exact-route only)",
                "readyForPhpRemoval=false (this reporter always — source keep)",
                "RequirePhpFallback stays true until dual-sample-green per exact route (templates default true)",
                "RELEASE_OWNER_APPROVAL.md present with APPROVED_TO_REMOVE_PHP_FALLBACK + KeepPhpProjectAvailable",
                "Named live tenants stay PHP-primary until unlocked parity shadows",
                "www classic entries may redirect to ASP.NET apps; PHP docroot stays for /index.php + deep modules"
            ],
            Note: _reference.Note);
    }

    private static string TrimBase(string url)
        => string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
}
