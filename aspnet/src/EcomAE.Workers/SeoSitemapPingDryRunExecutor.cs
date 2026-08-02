namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for SEO sitemap ping replacement. Outbound pings are always blocked.
/// </summary>
public sealed class SeoSitemapPingDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "seo-sitemap-ping", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_urls", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_urls as sitemap/ping target URLs (one per line).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["pings"] = "0",
                    ["valid_urls"] = "0",
                    ["invalid_urls"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            if (Uri.TryCreate(line, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                valid++;
            }
            else
            {
                invalid++;
            }
        }

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} sitemap/ping URL(s); outbound pings blocked."
            : "No valid absolute http(s) URLs found in sample_urls.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["pings"] = "0",
                ["valid_urls"] = valid.ToString(),
                ["invalid_urls"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample URLs were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
