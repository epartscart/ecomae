using System.Collections.Generic;
using System.Linq;
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
                    ? "validated surface digest smoke JSON with authenticated digest HTTP 200"
                    : "attach surface-digests-aspnet.json with ok=true and at least one non-migration digest HTTP 200"),
            Item("parity-samples-attached", "At least one attached PHP-vs-ASP.NET parity sample under evidence",
                PhpDecommissionEvidence.HasParitySamples(root)),
            Item("exact-route-shadows-only", "Exact-route nginx shadow examples present; broad cutover still forbidden",
                File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"))
                && File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-api-shadow-example.conf"))
                && File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"))
                && File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-storefront-digests-shadow-example.conf"))),
            Item("tenant-php-chrome-safe", "Live tenant/industry hosts remain PHP for frontend/CP/ERP (installer guards + probe)",
                HasTenantPhpChromeSafetyControls(),
                HasTenantPhpChromeSafetyControls()
                    ? "nginx site safety guards + tenant chrome probe present; run cloudpanel_probe_live_tenant_php_chrome.sh after any shadow change"
                    : "add scripts/ecomae_nginx_site_safety.py + cloudpanel_probe_live_tenant_php_chrome.sh; see docs/migration/TENANT_MIGRATION_SAFETY.md"),
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
        var smokePresent = PhpDecommissionEvidence.HasAuthenticatedPriceLookupSmoke(root)
            && PhpDecommissionEvidence.HasCatalogStatusSmoke(root)
            && PhpDecommissionEvidence.HasSurfaceDigestSmoke(root);
        var approvalPresent = PhpDecommissionEvidence.HasReleaseOwnerApproval(root);

        var extraBlockers = new List<string>
        {
            "Live tenant and industry hosts must keep PHP frontend/CP/ERP presentation and functionality; ASP.NET shadows default to www.ecomae.com only.",
            "Broad /, /api, /cp, /erp, /bos, and storefront nginx cutovers remain forbidden.",
            "PHP-FPM, PHP cron, PHP rewrites, and PHP source dependencies must remain until ReadyToRemovePhp is true."
        };
        if (!smokePresent)
        {
            extraBlockers.Insert(0, "Authenticated CloudPanel smoke keys/cookies are required; this agent cannot invent them.");
        }
        else if (!approvalPresent)
        {
            extraBlockers.Insert(0, "Human RELEASE_OWNER_APPROVAL.md with APPROVED_TO_REMOVE_PHP_FALLBACK is required; do not invent approval.");
        }

        var blockers = ready
            ? Array.Empty<string>()
            : missing.Concat(extraBlockers).ToArray();

        string[] nextActions;
        if (ready)
        {
            nextActions =
            [
                "ReadyToRemovePhp is true for exact-route fallback removal only.",
                "On CloudPanel run: ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh",
                "Keep rollback available: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback",
                "Do not enable broad /api /cp /erp /bos /storefront cutover."
            ];
        }
        else if (smokePresent && !approvalPresent)
        {
            nextActions =
            [
                "Staging smoke artifacts are attached (price lookup, catalog status, surface digests).",
                "Redeploy main so ContentRoot packs smoke evidence: bash scripts/cloudpanel_redeploy_final_gate_branch.sh",
                "Confirm /migration/php-decommission-readiness shows smoke checklist items present (approval still missing).",
                "Optional: promote one approved location= shadow via bash scripts/cloudpanel_extract_exact_route_shadow.sh (never broad cutover).",
                "Obtain human release-owner approval; create RELEASE_OWNER_APPROVAL.md with APPROVED_TO_REMOVE_PHP_FALLBACK only after that approval.",
                "Do not remove PHP-FPM/cron/rewrites until ReadyToRemovePhp is true."
            ];
        }
        else
        {
            nextActions =
            [
                "Keep PHP authoritative for all production traffic.",
                "Run bash scripts/run_zero_php_final_gate_checklist.sh.",
                "Diagnose: bash scripts/cloudpanel_diagnose_smoke_db.sh",
                "If CREATE denied: apply_epc_api_clients_ddl.sh (clpctl) or use_php_dp_config_as_tenant_registry.sh when PHP db already has the table.",
                "On CloudPanel: ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh",
                "Then: ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES bash scripts/cloudpanel_issue_smoke_credentials.sh",
                "Validate (redacted): bash scripts/cloudpanel_validate_final_gate_env.sh / cloudpanel_prepare_smoke_secrets.sh.",
                "Capture/commit: source /etc/ecomae-aspnet/platform.env && bash scripts/cloudpanel_capture_final_gate_artifacts.sh && bash scripts/cloudpanel_commit_final_gate_smoke.sh",
                "Promote only approved exact-route shadows one path at a time (cloudpanel_extract_exact_route_shadow.sh).",
                "Do not remove PHP-FPM/cron/rewrites until ReadyToRemovePhp is true with release-owner approval."
            ];
        }

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
                "Tenant chrome probe pass under docs/migration/evidence/tenant-safety/live-tenant-php-chrome.json (operator-run)",
                "Operator rollback command validation",
                "Release-owner APPROVED_TO_REMOVE_PHP_FALLBACK artifact"
            ],
            nextActions);
    }

    private bool HasTenantPhpChromeSafetyControls()
    {
        var repo = FindRepoRoot();
        return File.Exists(Path.Combine(repo, "scripts", "ecomae_nginx_site_safety.py"))
            && File.Exists(Path.Combine(repo, "scripts", "lib", "ecomae_nginx_site_safety.sh"))
            && File.Exists(Path.Combine(repo, "scripts", "cloudpanel_probe_live_tenant_php_chrome.sh"))
            && File.Exists(Path.Combine(repo, "docs", "migration", "TENANT_MIGRATION_SAFETY.md"));
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
