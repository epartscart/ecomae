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

        var pairs = new List<PhpReferenceComparePair>
        {
            new("marketing", $"{wwwPhp}/", $"{aspNet}/marketing/app", "php-reference-vs-aspnet-preview"),
            new("cp", $"{wwwPhp}/CP/", $"{aspNet}/cp/app", "php-reference-vs-aspnet-preview"),
            new("erp", $"{wwwPhp}/ERP/", $"{aspNet}/erp/app", "php-reference-vs-aspnet-preview"),
            new("bos", $"{wwwPhp}/BOS/", $"{aspNet}/bos/app", "php-reference-vs-aspnet-preview"),
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
                "Serve live product traffic from ASP.NET only after dual-sample + exact-route shadows + human RELEASE_OWNER_APPROVAL.md.",
                "Keep the PHP project/docroot installed as a reference host (or read-only clone) so previous screens/results remain visible.",
                "Use /migration/compare and /migration/php-reference-mode to open PHP vs ASP.NET side-by-side and record gaps.",
                "Run dual-sample compare_* scripts against ECOMAE_PHP_BASE_URL / configured WwwPhpBaseUrl while ASP.NET is primary.",
                "Do not delete PHP source until a separate decommission gate (ReadyToRemovePhp) — reference mode is not deletion.",
                "Rollback live traffic with: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback"
            ],
            HardLocks:
            [
                "cutoverAllowed=false (this reporter always — traffic still exact-route only)",
                "readyForPhpRemoval=false (this reporter always — source keep)",
                "RequirePhpFallback stays true in templates until CloudPanel exact-route promote",
                "RELEASE_OWNER_APPROVAL.md is human-owned (marker APPROVED_TO_REMOVE_PHP_FALLBACK + KeepPhpProjectAvailable)",
                "Named live tenants stay PHP-primary until unlocked parity shadows",
                "Reference PHP should be read-only / non-conflicting for writes after ASP.NET is primary"
            ],
            Note: _reference.Note);
    }

    private static string TrimBase(string url)
        => string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
}
