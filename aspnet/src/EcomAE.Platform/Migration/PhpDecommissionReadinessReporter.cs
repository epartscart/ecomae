using Microsoft.Extensions.Hosting;

namespace EcomAE.Platform.Migration;

public interface IPhpDecommissionReadinessReporter
{
    PhpDecommissionReadinessReport BuildReport();
}

public sealed record PhpDecommissionChecklistItem(
    string Id,
    string Description,
    string Status,
    string Detail);

public sealed record PhpDecommissionReadinessReport(
    string Status,
    bool ReadyToRemovePhp,
    int BlockerCount,
    int ChecklistCompleteCount,
    int ChecklistTotalCount,
    double ChecklistCompletePercent,
    IReadOnlyCollection<PhpDecommissionChecklistItem> Checklist,
    IReadOnlyCollection<string> Blockers,
    IReadOnlyCollection<string> RequiredEvidence,
    IReadOnlyCollection<string> NextActions);

/// <summary>
/// Tracks the final Zero-PHP gate via an evidence checklist.
/// ReadyToRemovePhp becomes true only when every checklist item is present with validated smoke/approval artifacts.
/// Broad /api /cp /erp /bos /storefront cutover remains forbidden even when ready.
/// </summary>
public sealed class PhpDecommissionReadinessReporter : IPhpDecommissionReadinessReporter
{
    private readonly IHostEnvironment _environment;

    public PhpDecommissionReadinessReporter(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public PhpDecommissionReadinessReport BuildReport()
    {
        var root = ResolveEvidenceRoot();
        var checklist = new[]
        {
            Item("public-probes", "Public production diagnostic probes attached (no secrets)",
                File.Exists(Path.Combine(root, "public-probes", "www-zero-php-completion.json"))
                && File.Exists(Path.Combine(root, "public-probes", "www-php-decommission-readiness.json"))),
            Item("staging-smoke-price", "Price lookup exact-route staging smoke artifact (authenticated 200)",
                PhpDecommissionEvidence.HasAuthenticatedPriceLookupSmoke(root),
                PhpDecommissionEvidence.HasAuthenticatedPriceLookupSmoke(root)
                    ? "validated authenticated smoke JSON"
                    : "attach authenticated 200 smoke under staging-smoke/price-lookup-aspnet.json"),
            Item("staging-smoke-catalog", "Catalog status exact-route staging smoke artifact",
                PhpDecommissionEvidence.HasCatalogStatusSmoke(root),
                PhpDecommissionEvidence.HasCatalogStatusSmoke(root)
                    ? "validated catalog smoke JSON"
                    : "attach authenticated smoke under staging-smoke/catalog-status-aspnet.json"),
            Item("staging-smoke-surfaces", "CP/ERP/BOS digest staging smoke artifact",
                PhpDecommissionEvidence.HasSurfaceDigestSmoke(root),
                PhpDecommissionEvidence.HasSurfaceDigestSmoke(root)
                    ? "validated surface digest smoke JSON"
                    : "attach surface-digests-aspnet.json with ok=true and routes[]"),
            Item("parity-samples-attached", "At least one attached PHP-vs-ASP.NET parity sample under evidence",
                PhpDecommissionEvidence.HasParitySamples(root)),
            Item("exact-route-shadows-only", "Exact-route nginx shadow examples present; broad cutover still forbidden",
                File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"))
                && File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"))),
            Item("cloudpanel-capture-script", "CloudPanel final-gate capture script exists",
                File.Exists(Path.Combine(FindRepoRoot(), "scripts", "cloudpanel_capture_final_gate_artifacts.sh"))),
            Item("rollback-validated", "Operator rollback script exists and keeps PHP fallback",
                File.Exists(Path.Combine(FindRepoRoot(), "scripts", "rollback_aspnet_foundation.sh"))),
            Item("release-owner-approval", "Release-owner written approval artifact",
                PhpDecommissionEvidence.HasReleaseOwnerApproval(root),
                PhpDecommissionEvidence.HasReleaseOwnerApproval(root)
                    ? "APPROVED_TO_REMOVE_PHP_FALLBACK marker present"
                    : "create RELEASE_OWNER_APPROVAL.md with APPROVED_TO_REMOVE_PHP_FALLBACK after smoke")
        };

        var complete = checklist.Count(item => string.Equals(item.Status, "present", StringComparison.OrdinalIgnoreCase));
        var missing = checklist
            .Where(item => string.Equals(item.Status, "missing", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Id}: {item.Description}")
            .ToArray();

        var ready = complete == checklist.Length && missing.Length == 0;
        var blockers = ready
            ? Array.Empty<string>()
            : missing.Concat([
                "Authenticated CloudPanel smoke keys/cookies are required; this agent cannot invent them.",
                "Broad /, /api, /cp, /erp, /bos, and storefront nginx cutovers remain forbidden.",
                "PHP-FPM, PHP cron, PHP rewrites, and PHP source dependencies must remain until ReadyToRemovePhp is true."
            ]).ToArray();

        return new PhpDecommissionReadinessReport(
            ready ? "ready-for-php-removal" : "blocked-not-ready-for-php-removal",
            ReadyToRemovePhp: ready,
            blockers.Length,
            complete,
            checklist.Length,
            checklist.Length == 0 ? 0 : Math.Round(100.0 * complete / checklist.Length, 1),
            checklist,
            blockers,
            [
                "Green PHP-vs-ASP.NET parity samples for tracked routes under evidence/parity-samples/",
                "Staging smoke artifacts for exact-route proxies under docs/migration/evidence/decommission/staging-smoke/",
                "Operator rollback command validation",
                "Release-owner APPROVED_TO_REMOVE_PHP_FALLBACK artifact"
            ],
            ready
                ?
                [
                    "ReadyToRemovePhp is true for exact-route fallback removal only.",
                    "On CloudPanel run: ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh",
                    "Keep rollback available: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback",
                    "Do not enable broad /api /cp /erp /bos /storefront cutover."
                ]
                :
                [
                    "Keep PHP authoritative for all production traffic.",
                    "Run bash scripts/run_zero_php_final_gate_checklist.sh.",
                    "On CloudPanel: add ECOMAE_PRICE_LOOKUP_API_KEY / ECOMAE_CATALOG_API_KEY / ECOMAE_ADMIN_COOKIE_HEADER to platform.env, then bash scripts/cloudpanel_capture_final_gate_artifacts.sh.",
                    "Copy generated smoke JSON into docs/migration/evidence/decommission/staging-smoke/ after staging runs.",
                    "Promote only approved exact-route shadows one path at a time.",
                    "Do not remove PHP-FPM/cron/rewrites until ReadyToRemovePhp is true with release-owner approval."
                ]);
    }

    private static PhpDecommissionChecklistItem Item(string id, string description, bool present, string? detail = null)
        => new(
            id,
            description,
            present ? "present" : "missing",
            detail ?? (present ? "evidence located" : "attach evidence before considering PHP removal"));

    private string ResolveEvidenceRoot()
    {
        foreach (var candidate in EvidenceRootCandidates())
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(_environment.ContentRootPath, "docs", "migration", "evidence", "decommission");
    }

    private IEnumerable<string> EvidenceRootCandidates()
    {
        yield return Path.Combine(_environment.ContentRootPath, "docs", "migration", "evidence", "decommission");
        var repo = FindRepoRoot();
        if (!string.IsNullOrWhiteSpace(repo))
        {
            yield return Path.Combine(repo, "docs", "migration", "evidence", "decommission");
        }
    }

    private string FindRepoRoot()
    {
        foreach (var start in new[] { _environment.ContentRootPath, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "scripts", "run_zero_php_final_gate_checklist.sh")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return _environment.ContentRootPath;
    }
}
