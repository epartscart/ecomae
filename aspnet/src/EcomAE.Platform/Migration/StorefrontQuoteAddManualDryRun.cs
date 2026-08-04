namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_add_to_quote_manual.php</c> (brand+article → draft quote).
/// Never executes INSERT. Guests denied. PHP remains authoritative.
/// </summary>
public interface IStorefrontQuoteAddManualDryRun
{
    Task<StorefrontQuoteAddManualDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteAddManualRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontQuoteAddManualDryRun : IStorefrontQuoteAddManualDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontQuoteAddManualDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontQuoteAddManualDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteAddManualRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote add-manual is not implemented; PHP ajax_add_to_quote_manual.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP guest commerce denied).", request);
        }

        var digest = await _dashboards.BuildCpQuoteRequestsDigestAsync(200, cancellationToken);
        return EvaluateAgainstQuotes(userId, digest.Quotes, request);
    }

    public static StorefrontQuoteAddManualDryRunResult EvaluateAgainstQuotes(
        int userId,
        IReadOnlyList<CpQuoteRequestsRowDigest> quotes,
        StorefrontQuoteAddManualRequest request)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote add-manual is not implemented; PHP ajax_add_to_quote_manual.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP guest commerce denied).", request);
        }

        var manufacturer = (request.Manufacturer ?? string.Empty).Trim();
        var article = (request.Article ?? string.Empty).Trim();
        if (manufacturer.Length == 0 || article.Length == 0)
        {
            return Refuse("dry-run-invalid", "brand_article_required",
                "Brand and part number are required (PHP).", request);
        }

        var countNeed = request.CountNeed < 1 ? 1 : request.CountNeed;

        var draft = quotes
            .Where(q => q.UserId == userId && string.Equals(q.Status, "draft", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(q => q.Id)
            .FirstOrDefault();

        var wouldCreateDraft = draft is null;
        var quoteId = draft?.Id ?? 0;

        return new StorefrontQuoteAddManualDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            QuoteId: quoteId,
            UserId: userId,
            WouldCreateDraft: wouldCreateDraft,
            Manufacturer: manufacturer,
            Article: article,
            CountNeed: countNeed,
            SimulatedSql:
            [
                wouldCreateDraft
                    ? "INSERT INTO `shop_quote_requests` (`user_id`, `session_id`, `status`, `time_created`, `time_updated`) VALUES (@user,0,'draft',@now,@now) (NOT executed)"
                    : $"Reuse draft quote_id={quoteId} (NOT executed)",
                "INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (@quoteId, 2, @manual_json, @count) (NOT executed)",
                "epc_manual_quote=1 / check_hash=manual stay PHP-shaped until dual-sample"
            ],
            Detail: wouldCreateDraft
                ? "No draft in digest window; create-draft + manual item INSERT simulated."
                : $"Draft quote {quoteId} found; manual item INSERT simulated.",
            PhpAjax: "/content/shop/order_process/ajax_add_to_quote_manual.php");
    }

    private static StorefrontQuoteAddManualDryRunResult Refuse(
        string status, string code, string detail, StorefrontQuoteAddManualRequest request) =>
        new(status, 0, true, false, true, code, false, 0, 0, false,
            request.Manufacturer, request.Article, request.CountNeed, [], detail,
            "/content/shop/order_process/ajax_add_to_quote_manual.php");
}

public sealed record StorefrontQuoteAddManualRequest(
    string? Manufacturer,
    string? Article,
    int CountNeed = 1,
    bool ConfirmWrites = false);

public sealed record StorefrontQuoteAddManualDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long QuoteId, int UserId, bool WouldCreateDraft,
    string? Manufacturer, string? Article, int CountNeed,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "storefront",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new
        {
            quote_id = QuoteId,
            would_create_draft = WouldCreateDraft,
            manufacturer = Manufacturer,
            article = Article,
            count_need = CountNeed,
            epc_manual_quote = 1
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
