using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Storefront Excel / CSV price check — PHP <c>content/shop/bulk_upload/ajax_process.php</c> twin.
/// Matches warehouse offers through <see cref="ISurfaceDashboardSummaryReporter.SearchStorefrontPartsAsync"/>.
/// History INSERT stays on the PHP ajax when the shop DB is not configured here.
/// </summary>
public interface IStorefrontBulkUploadCheckService
{
    Task<StorefrontBulkUploadCheckResult> ProcessAsync(
        Stream file,
        string fileName,
        string priority,
        CancellationToken cancellationToken = default);

    Task<StorefrontBulkUploadCrossResult> CrossAsync(
        string article,
        int qty,
        string priority,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontBulkUploadCheckService : IStorefrontBulkUploadCheckService
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontBulkUploadCheckService(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<StorefrontBulkUploadCheckResult> ProcessAsync(
        Stream file,
        string fileName,
        string priority,
        CancellationToken cancellationToken = default)
    {
        var items = StorefrontBulkUploadFileParser.Read(file, fileName, out var parseError);
        if (items.Count == 0)
        {
            return Fail(parseError ?? "Upload file is required.");
        }

        var safePriority = string.Equals(priority, "delivery", StringComparison.OrdinalIgnoreCase)
            ? "delivery"
            : "price";
        var rows = new StorefrontBulkUploadRow[items.Count];
        var source = "database";
        using var gate = new SemaphoreSlim(4, 4);
        var tasks = items.Select(async (item, index) =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (exact, cross, rowSource) = await MatchAsync(item, safePriority, includeCross: false, cancellationToken)
                    .ConfigureAwait(false);
                rows[index] = StorefrontBulkUploadMatcher.BuildRow(item, exact, cross, crossChecked: false);
                if (!string.Equals(rowSource, "database", StringComparison.Ordinal))
                {
                    source = rowSource;
                }
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);

        var (summary, csv) = StorefrontBulkUploadMatcher.Summarize(rows);
        return new StorefrontBulkUploadCheckResult(true, null, rows, summary, csv, source);
    }

    public async Task<StorefrontBulkUploadCrossResult> CrossAsync(
        string article,
        int qty,
        string priority,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(article))
        {
            return new StorefrontBulkUploadCrossResult(false, "Part number is required.", null, null);
        }

        var item = new StorefrontBulkUploadLine("", article.Trim(), qty > 0 ? qty : 1, "", "", "");
        var safePriority = string.Equals(priority, "delivery", StringComparison.OrdinalIgnoreCase)
            ? "delivery"
            : "price";
        var (exact, cross, _) = await MatchAsync(item, safePriority, includeCross: true, cancellationToken)
            .ConfigureAwait(false);
        return new StorefrontBulkUploadCrossResult(true, null, exact, cross);
    }

    private async Task<(StorefrontBulkUploadOffer? Exact, StorefrontBulkUploadOffer? Cross, string Source)> MatchAsync(
        StorefrontBulkUploadLine item,
        string priority,
        bool includeCross,
        CancellationToken cancellationToken)
    {
        var brand = item.Brand.Trim();
        var exactSearch = await _dashboards
            .SearchStorefrontPartsAsync(item.Article, brand.Length > 0 ? brand : null, 40, cancellationToken)
            .ConfigureAwait(false);
        var source = exactSearch.Source;
        var exactDigest = StorefrontBulkUploadMatcher.PickBest(exactSearch.Rows, priority);
        StorefrontBulkUploadOffer? exact = exactDigest is null
            ? null
            : StorefrontBulkUploadMatcher.ToOffer(exactDigest, item.Qty, "exact", "Exact", selected: true);

        StorefrontBulkUploadOffer? cross = null;
        if (includeCross)
        {
            var crossSearch = brand.Length == 0
                ? exactSearch
                : await _dashboards.SearchStorefrontPartsAsync(item.Article, null, 40, cancellationToken)
                    .ConfigureAwait(false);
            if (!string.Equals(crossSearch.Source, "database", StringComparison.Ordinal))
            {
                source = crossSearch.Source;
            }

            var candidates = crossSearch.Rows
                .Where(r => exactDigest is null || !StorefrontBulkUploadMatcher.SameOffer(r, exactDigest))
                .ToList();
            var crossDigest = StorefrontBulkUploadMatcher.PickBest(candidates, priority);
            if (crossDigest is not null)
            {
                cross = StorefrontBulkUploadMatcher.ToOffer(crossDigest, item.Qty, "cross", "Related", selected: exact is null);
                if (exact is not null)
                {
                    exact = exact with { Selected = true };
                }
            }
        }

        return (exact, cross, source);
    }

    private static StorefrontBulkUploadCheckResult Fail(string message)
        => new(false, message, [], new StorefrontBulkUploadSummary(0, 0, 0, 0, 0), "", "error");
}
