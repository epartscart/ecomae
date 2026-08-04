namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_add_to_quote.php</c> (type-2 line → draft quote).
/// Never executes INSERT. check_hash / product_object_json stay PHP-authoritative.
/// </summary>
public interface IStorefrontQuoteAddItemDryRun
{
    Task<StorefrontQuoteAddItemDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteAddItemRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontQuoteAddItemDryRun : IStorefrontQuoteAddItemDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontQuoteAddItemDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontQuoteAddItemDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteAddItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote add-item is not implemented; PHP ajax_add_to_quote.php remains authoritative.",
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

    public static StorefrontQuoteAddItemDryRunResult EvaluateAgainstQuotes(
        int userId,
        IReadOnlyList<CpQuoteRequestsRowDigest> quotes,
        StorefrontQuoteAddItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote add-item is not implemented; PHP ajax_add_to_quote.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP guest commerce denied).", request);
        }

        if (request.ProductType != 2)
        {
            return Refuse("dry-run-invalid", "product_type_unsupported",
                "Only product_type=2 (supplier price-search) lines can be added to a quote.", request);
        }

        var manufacturer = (request.Manufacturer ?? string.Empty).Trim();
        var article = (request.Article ?? string.Empty).Trim();
        if (manufacturer.Length == 0 || article.Length == 0)
        {
            return Refuse("dry-run-invalid", "incorrect_data",
                "manufacturer and article are required.", request);
        }

        if (request.CountNeed <= 0)
        {
            return Refuse("dry-run-invalid", "incorrect_data", "countNeed must be > 0.", request);
        }

        var draft = quotes
            .Where(q => q.UserId == userId && string.Equals(q.Status, "draft", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(q => q.Id)
            .FirstOrDefault();

        var wouldCreateDraft = draft is null;
        var quoteId = draft?.Id ?? 0;

        return new StorefrontQuoteAddItemDryRunResult(
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
            CountNeed: request.CountNeed,
            SimulatedSql:
            [
                wouldCreateDraft
                    ? "INSERT INTO `shop_quote_requests` (`user_id`, `session_id`, `status`, `time_created`, `time_updated`) VALUES (@user,0,'draft',@now,@now) (NOT executed)"
                    : $"Reuse draft quote_id={quoteId} (NOT executed)",
                "INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`) VALUES (@quoteId, 2, @json, @count) (NOT executed)",
                "check_hash / tech_key validation remains PHP-only in this dry-run slice"
            ],
            Detail: wouldCreateDraft
                ? "No draft in digest window; create-draft + item INSERT simulated. check_hash stays PHP."
                : $"Draft quote {quoteId} found; item INSERT simulated. check_hash stays PHP.",
            PhpAjax: "/content/shop/order_process/ajax_add_to_quote.php");
    }

    private static StorefrontQuoteAddItemDryRunResult Refuse(
        string status, string code, string detail, StorefrontQuoteAddItemRequest request) =>
        new(status, 0, true, false, true, code, false, 0, 0, false,
            request.Manufacturer, request.Article, request.CountNeed, [], detail,
            "/content/shop/order_process/ajax_add_to_quote.php");
}

public sealed record StorefrontQuoteAddItemRequest(
    int ProductType,
    string? Manufacturer,
    string? Article,
    int CountNeed = 1,
    bool ConfirmWrites = false);

public sealed record StorefrontQuoteAddItemDryRunResult(
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
            product_type = 2,
            manufacturer = Manufacturer,
            article = Article,
            count_need = CountNeed
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
