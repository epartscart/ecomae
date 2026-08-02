namespace EcomAE.Platform.Migration;

public interface IPhpDecommissionReadinessReporter
{
    PhpDecommissionReadinessReport BuildReport();
}

public sealed record PhpDecommissionReadinessReport(
    string Status,
    bool ReadyToRemovePhp,
    int BlockerCount,
    IReadOnlyCollection<string> Blockers,
    IReadOnlyCollection<string> RequiredEvidence,
    IReadOnlyCollection<string> NextActions);

/// <summary>
/// Documents why PHP runtime decommission remains blocked. Never authorizes PHP removal.
/// </summary>
public sealed class PhpDecommissionReadinessReporter : IPhpDecommissionReadinessReporter
{
    public PhpDecommissionReadinessReport BuildReport()
    {
        string[] blockers =
        [
            "Route/job parity-ready remains 0%; dry-run scaffolding is not live cutover.",
            "Exact-route staging smoke artifacts are not attached for price/catalog or surface digests.",
            "No approved location= nginx shadows have been promoted from examples to production.",
            "Batches 1-61 still require per-entry parity samples before PHP fallback removal.",
            "Release-owner decommission approval is missing.",
            "PHP-FPM, PHP cron, PHP rewrites, and PHP source dependencies must remain until the final gate."
        ];

        return new PhpDecommissionReadinessReport(
            "blocked-not-ready-for-php-removal",
            ReadyToRemovePhp: false,
            blockers.Length,
            blockers,
            [
                "Green PHP-vs-ASP.NET parity samples for every tracked route/job",
                "Staging smoke artifacts for exact-route proxies",
                "Operator rollback command validation",
                "Release-owner written approval to disable PHP fallback and remove PHP runtime"
            ],
            [
                "Keep PHP authoritative for all production traffic.",
                "Run exact-route staging smoke with real API keys and admin/customer sessions.",
                "Promote only approved exact-route shadows one path at a time.",
                "Do not remove PHP-FPM/cron/rewrites until every tracked item is live or removed."
            ]);
    }
}
