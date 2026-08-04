namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_quote_accept.php</c> (quoted → accepted).
/// Never executes UPDATE/INSERT. Cart line INSERTs stay PHP-authoritative.
/// </summary>
public interface IStorefrontQuoteAcceptDryRun
{
    Task<StorefrontQuoteAcceptDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteAcceptRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontQuoteAcceptDryRun : IStorefrontQuoteAcceptDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontQuoteAcceptDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontQuoteAcceptDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteAcceptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote accept is not implemented; PHP ajax_quote_accept.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP guest commerce denied).", request);
        }

        if (request.QuoteId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "quoteId must be positive.", request);
        }

        var digest = await _dashboards.BuildCpQuoteRequestsDigestAsync(200, cancellationToken);
        return EvaluateAgainstQuotes(userId, digest.Quotes, request);
    }

    public static StorefrontQuoteAcceptDryRunResult EvaluateAgainstQuotes(
        int userId,
        IReadOnlyList<CpQuoteRequestsRowDigest> quotes,
        StorefrontQuoteAcceptRequest request)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote accept is not implemented; PHP ajax_quote_accept.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP guest commerce denied).", request);
        }

        if (request.QuoteId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "quoteId must be positive.", request);
        }

        var quote = quotes.FirstOrDefault(q => q.Id == request.QuoteId && q.UserId == userId);
        if (quote is null)
        {
            return Refuse("dry-run-invalid", "quote_not_in_digest_window",
                $"Quote {request.QuoteId} not found for user {userId} in recent quote-requests digest window.",
                request);
        }

        if (!string.Equals(quote.Status, "quoted", StringComparison.OrdinalIgnoreCase))
        {
            return Refuse("dry-run-invalid", "quote_not_quoted",
                $"Quote status '{quote.Status}' — expected quoted (PHP accept gate).", request);
        }

        return new StorefrontQuoteAcceptDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            QuoteId: quote.Id,
            UserId: userId,
            QuoteStatus: quote.Status,
            SimulatedSql:
            [
                "UPDATE `shop_quote_requests` SET `status`='accepted', `time_updated`=@now WHERE `id`=@id AND `user_id`=@user (NOT executed)",
                "INSERT INTO `shop_carts` (…) per quote line remains PHP-only in this dry-run slice (product_object / alt pricing / check_hash)",
                "Line-count + quoted_price completeness gates remain PHP-only in this dry-run slice"
            ],
            Detail: "Owned quoted request found in digest window; accept UPDATE simulated. Cart INSERTs stay PHP until dual-sample.",
            PhpAjax: "/content/shop/order_process/ajax_quote_accept.php");
    }

    private static StorefrontQuoteAcceptDryRunResult Refuse(
        string status, string code, string detail, StorefrontQuoteAcceptRequest request) =>
        new(status, 0, true, false, true, code, false, request.QuoteId, 0, null,
            [], detail,
            "/content/shop/order_process/ajax_quote_accept.php");
}

public sealed record StorefrontQuoteAcceptRequest(
    long QuoteId,
    bool ConfirmWrites = false);

public sealed record StorefrontQuoteAcceptDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long QuoteId, int UserId, string? QuoteStatus,
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
        intended = new { quote_id = QuoteId },
        current = QuoteStatus is null ? null : new { status = QuoteStatus, user_id = UserId },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
