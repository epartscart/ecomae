namespace EcomAE.Workers;

/// <summary>
/// Batch 5 dry-run for catalog miss→UMAPI fill intent.
/// Simulates fill targets only — outbound HTTP, UMAPI calls, and cache writes stay blocked.
/// Live fills remain PHP-authoritative (<c>api/umapi_proxy.php</c> / <c>api/v1/catalog.php</c>).
/// </summary>
public sealed class CatalogMissFillDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    /// <summary>Mirrors PHP <c>epc_cacheable_action</c> (fill-on-miss candidates).</summary>
    public static readonly HashSet<string> CacheableActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "manufacturers", "models", "modifications", "categories", "products",
        "suppliers", "article", "vin", "engines", "engine_search", "brands",
        "analogs", "article_links"
    };

    /// <summary>Always-live PHP actions — never cache-fill candidates in this dry-run.</summary>
    public static readonly HashSet<string> AlwaysLiveActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "articles", "engine"
    };

    private static readonly Dictionary<string, string> AspNetRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["manufacturers"] = "/api/v1/catalog/manufacturers",
        ["models"] = "/api/v1/catalog/models",
        ["modifications"] = "/api/v1/catalog/modifications",
        ["categories"] = "/api/v1/catalog/categories",
        ["products"] = "/api/v1/catalog/products",
        ["suppliers"] = "/api/v1/catalog/suppliers",
        ["article"] = "/api/v1/catalog/article",
        ["vin"] = "/api/v1/catalog/vin",
        ["engines"] = "/api/v1/catalog/engines",
        ["engine_search"] = "/api/v1/catalog/engine-search",
        ["brands"] = "/api/v1/catalog/brands",
        ["analogs"] = "/api/v1/catalog/analogs",
        ["article_links"] = "/api/v1/catalog/article-links",
    };

    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "catalog-miss-fill", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var confirmOutbound = IsTruthy(parameters, "confirm_outbound");
        var confirmWrites = IsTruthy(parameters, "confirm_writes");

        if (confirmOutbound || confirmWrites)
        {
            const string warning =
                "confirm_outbound/confirm_writes requested but live ASP.NET miss-fill is not implemented; outbound and writes remain blocked (PHP authoritative).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-confirm-refused",
                warning,
                BlockedMetrics(valid: 0, invalid: 0, alwaysLive: 0),
                [warning],
                WritesBlocked: true);
        }

        if (!parameters.TryGetValue("sample_actions", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning =
                "Provide parameters.sample_actions as cacheable actions (one per line; optional action,query).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                BlockedMetrics(valid: 0, invalid: 0, alwaysLive: 0),
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        var alwaysLive = 0;
        var simulated = new List<string>();

        foreach (var line in lines)
        {
            var actionPart = line.Split(',', 2)[0].Trim().Replace('-', '_');
            if (AlwaysLiveActions.Contains(actionPart))
            {
                alwaysLive++;
                continue;
            }

            if (!CacheableActions.Contains(actionPart))
            {
                invalid++;
                continue;
            }

            valid++;
            var route = AspNetRoutes.TryGetValue(actionPart, out var r) ? r : $"/api/v1/catalog/{actionPart.Replace('_', '-')}";
            var missCode = string.Equals(actionPart, "vin", StringComparison.OrdinalIgnoreCase)
                ? "vin_cache_miss"
                : "cache_miss";
            var cacheTable = string.Equals(actionPart, "vin", StringComparison.OrdinalIgnoreCase)
                ? "epc_umapi_vin_cache"
                : "epc_umapi_cache";
            simulated.Add($"{actionPart}->{route}|{missCode}|{cacheTable}|php=api/umapi_proxy.php|executed=false");
        }

        var warnings = new List<string>();
        if (invalid > 0)
        {
            warnings.Add("Some sample_actions were unknown and skipped.");
        }

        if (alwaysLive > 0)
        {
            warnings.Add("articles/engine are always-live PHP actions; not simulated as cache fills.");
        }

        warnings.Add("Outbound UMAPI and cache writes blocked; PHP remains authoritative for live fills.");

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Simulated {valid} miss-fill intent(s); outbound=0 writes=0 fills=0 (PHP authoritative)."
            : "No valid cacheable actions found in sample_actions.";

        var metrics = BlockedMetrics(valid, invalid, alwaysLive);
        metrics["simulated"] = Math.Min(simulated.Count, 3).ToString();
        if (simulated.Count > 0)
        {
            metrics["sample0"] = simulated[0];
        }

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            metrics,
            warnings,
            WritesBlocked: true);
    }

    private static Dictionary<string, string> BlockedMetrics(int valid, int invalid, int alwaysLive) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["outbound"] = "0",
            ["writes"] = "0",
            ["fills"] = "0",
            ["warms"] = "0",
            ["valid_actions"] = valid.ToString(),
            ["invalid_actions"] = invalid.ToString(),
            ["always_live_rejected"] = alwaysLive.ToString(),
            ["php_authoritative"] = "true",
            ["cutover_allowed"] = "false"
        };

    private static bool IsTruthy(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters.TryGetValue(key, out var value)
        && (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));
}
