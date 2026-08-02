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
/// Tracks the final Zero-PHP gate via an evidence checklist. Never authorizes PHP removal.
/// ReadyToRemovePhp remains false until every checklist item is present and release-owner approval is attached.
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
                File.Exists(Path.Combine(root, "staging-smoke", "price-lookup-aspnet.json"))),
            Item("staging-smoke-catalog", "Catalog status exact-route staging smoke artifact",
                File.Exists(Path.Combine(root, "staging-smoke", "catalog-status-aspnet.json"))),
            Item("staging-smoke-surfaces", "CP/ERP/BOS digest staging smoke artifact",
                File.Exists(Path.Combine(root, "staging-smoke", "surface-digests-aspnet.json"))),
            Item("parity-samples-attached", "At least one attached PHP-vs-ASP.NET parity sample under evidence",
                Directory.Exists(Path.Combine(root, "parity-samples"))
                && Directory.EnumerateFiles(Path.Combine(root, "parity-samples"), "*.json", SearchOption.AllDirectories).Any()),
            Item("exact-route-shadows-only", "Exact-route nginx shadow examples present; broad cutover still forbidden",
                File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"))
                && File.Exists(Path.Combine(FindRepoRoot(), "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"))),
            Item("cloudpanel-capture-script", "CloudPanel final-gate capture script exists",
                File.Exists(Path.Combine(FindRepoRoot(), "scripts", "cloudpanel_capture_final_gate_artifacts.sh"))),
            Item("rollback-validated", "Operator rollback script exists and keeps PHP fallback",
                File.Exists(Path.Combine(FindRepoRoot(), "scripts", "rollback_aspnet_foundation.sh"))),
            Item("release-owner-approval", "Release-owner written approval artifact",
                File.Exists(Path.Combine(root, "RELEASE_OWNER_APPROVAL.md"))
                && File.ReadAllText(Path.Combine(root, "RELEASE_OWNER_APPROVAL.md"))
                    .Contains("APPROVED_TO_REMOVE_PHP_FALLBACK", StringComparison.Ordinal))
        };

        var complete = checklist.Count(item => string.Equals(item.Status, "present", StringComparison.OrdinalIgnoreCase));
        var blockers = checklist
            .Where(item => string.Equals(item.Status, "missing", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Id}: {item.Description}")
            .Concat([
                "Route/job parity-ready and shadow-or-better remain 0% until live evidence is attached.",
                "PHP-FPM, PHP cron, PHP rewrites, and PHP source dependencies must remain until the final gate."
            ])
            .ToArray();

        // Hard rule: never authorize PHP removal from scaffolding alone.
        var ready = false;

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
                "Green PHP-vs-ASP.NET parity samples for every tracked route/job",
                "Staging smoke artifacts for exact-route proxies under docs/migration/evidence/decommission/staging-smoke/",
                "Operator rollback command validation",
                "Release-owner APPROVED_TO_REMOVE_PHP_FALLBACK artifact"
            ],
            [
                "Keep PHP authoritative for all production traffic.",
                "Run bash scripts/run_zero_php_final_gate_checklist.sh.",
                "On CloudPanel: add ECOMAE_PRICE_LOOKUP_API_KEY / ECOMAE_CATALOG_API_KEY to platform.env, then bash scripts/cloudpanel_capture_final_gate_artifacts.sh.",
                "Copy generated smoke JSON into docs/migration/evidence/decommission/staging-smoke/ after staging runs.",
                "Promote only approved exact-route shadows one path at a time.",
                "Do not remove PHP-FPM/cron/rewrites until ReadyToRemovePhp is true with release-owner approval."
            ]);
    }

    private static PhpDecommissionChecklistItem Item(string id, string description, bool present)
        => new(
            id,
            description,
            present ? "present" : "missing",
            present ? "evidence located" : "attach evidence before considering PHP removal");

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
        yield return Path.Combine(FindRepoRoot(), "docs", "migration", "evidence", "decommission");
    }

    private string FindRepoRoot()
    {
        var current = new DirectoryInfo(_environment.ContentRootPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "docs", "migration", "PHP_DECOMMISSION_READINESS.md"))
                || File.Exists(Path.Combine(current.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(current.FullName, "scripts", "cloudpanel_capture_final_gate_artifacts.sh")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return _environment.ContentRootPath;
    }
}
