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

        // Tenant-shared URLs (/cp /erp /bos /) stay unchanged and serve ASP.NET.
        // PHP reference is SEPARATE under /php-reference/* (plus dedicated CP host / tenant deep paths).
        var pairs = new List<PhpReferenceComparePair>
        {
            new("marketing", $"{wwwPhp}/php-reference/home", $"{aspNet}/", "php-reference-vs-aspnet-shared-home"),
            new("cp", $"{wwwPhp}/php-reference/cp", $"{aspNet}/cp", "php-reference-vs-aspnet-shared-cp"),
            new("erp", $"{wwwPhp}/php-reference/erp", $"{aspNet}/erp", "php-reference-vs-aspnet-shared-erp"),
            new("bos", $"{wwwPhp}/php-reference/bos", $"{aspNet}/bos", "php-reference-vs-aspnet-shared-bos"),
            new("cp-dedicated", $"{dedicatedCp}/CP/", $"{aspNet}/cp", "php-reference-vs-aspnet-shared-cp"),
            new("tenant-storefront", $"{tenantPhp}/php-reference/storefront", $"{tenantPhp}/", "php-reference-vs-aspnet-shared-tenant-home"),
            new("tenant-cp", $"{tenantPhp}/php-reference/cp", $"{tenantPhp}/cp", "php-reference-vs-aspnet-shared-tenant-cp"),
            new("tenant-erp", $"{tenantPhp}/php-reference/erp", $"{tenantPhp}/erp", "php-reference-vs-aspnet-shared-tenant-erp"),
        };

        var status = _reference.TemporarilyDeactivatePhpServing
            ? (_reference.KeepPhpProjectAvailable
                ? "aspnet-only-deep-test-php-serving-deactivated"
                : "php-serving-deactivated-misconfigured-keep-project-false")
            : !_reference.Enabled
                ? "php-reference-disabled"
                : _reference.KeepPhpProjectAvailable
                    ? "aspnet-primary-intent-php-reference-retained"
                    : "php-reference-misconfigured-keep-project-false";

        var mode = _reference.TemporarilyDeactivatePhpServing
            ? "aspnet-only-deep-test-php-serving-off"
            : (string.IsNullOrWhiteSpace(_reference.Mode) ? "aspnet-primary-php-reference" : _reference.Mode);

        return new PhpReferenceModeReport(
            Status: status,
            Mode: mode,
            Enabled: _reference.Enabled,
            ArchitectureConfirmed: _reference.ArchitectureConfirmed,
            KeepPhpProjectAvailable: _reference.KeepPhpProjectAvailable,
            TemporarilyDeactivatePhpServing: _reference.TemporarilyDeactivatePhpServing,
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
                "ALL product tenants → ASP.NET (URL preserved): ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts",
                "ASP.NET product: / /cp /erp /bos on www.ecomae.com + epartscart + electronicae + stylenlook + thejewellerytrend + taxofinca",
                "TEMP deep-test (pause PHP serving, keep files): ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING=YES bash scripts/cloudpanel_temporarily_deactivate_php_serving.sh",
                "Restore PHP reference serving: ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh",
                "PHP reference SEPARATE only: /php-reference/home|/cp|/erp|/bos|/storefront — compare at /migration/compare (never mix into product)",
                "Do not delete PHP source until a separate decommission gate (ReadyToRemovePhp) — reference mode / temp deactivate ≠ deletion.",
                "Rollback live traffic with: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback"
            ],
            HardLocks:
            [
                "cutoverAllowed=false (this reporter always — traffic still exact-route only)",
                "readyForPhpRemoval=false (this reporter always — source keep)",
                "TemporarilyDeactivatePhpServing pauses serving only; KeepPhpProjectAvailable must stay true",
                "RequirePhpFallback stays true until dual-sample-green per exact route (templates default true)",
                "RELEASE_OWNER_APPROVAL.md present with APPROVED_TO_REMOVE_PHP_FALLBACK + KeepPhpProjectAvailable",
                "Tenant-shared /cp /erp /bos / URLs must not redirect to /cp/app (URL preserved)",
                "No new PHP feature development — ASP.NET Core + related tools only; never invent cutoverAllowed=true"
            ],
            Note: _reference.Note);
    }

    private static string TrimBase(string url)
        => string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim().TrimEnd('/');
}
