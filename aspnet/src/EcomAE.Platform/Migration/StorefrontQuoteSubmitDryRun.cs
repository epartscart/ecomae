namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_quote_submit.php</c> (draft → submitted).
/// Never executes UPDATE. Line-count gate stays PHP-authoritative.
/// </summary>
public interface IStorefrontQuoteSubmitDryRun
{
    Task<StorefrontQuoteSubmitDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteSubmitRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontQuoteSubmitDryRun : IStorefrontQuoteSubmitDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontQuoteSubmitDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontQuoteSubmitDryRunResult> EvaluateAsync(
        int userId,
        StorefrontQuoteSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote submit is not implemented; PHP ajax_quote_submit.php remains authoritative.",
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

    public static StorefrontQuoteSubmitDryRunResult EvaluateAgainstQuotes(
        int userId,
        IReadOnlyList<CpQuoteRequestsRowDigest> quotes,
        StorefrontQuoteSubmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET quote submit is not implemented; PHP ajax_quote_submit.php remains authoritative.",
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

        if (!string.Equals(quote.Status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return Refuse("dry-run-invalid", "quote_not_draft",
                $"Quote status '{quote.Status}' — expected draft (PHP already submitted).", request);
        }

        var note = (request.CustomerNote ?? string.Empty).Trim();
        if (note.Length > 2000)
        {
            note = note[..2000];
        }

        return new StorefrontQuoteSubmitDryRunResult(
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
            CustomerNote: note.Length == 0 ? null : note,
            SimulatedSql:
            [
                "UPDATE `shop_quote_requests` SET `status`='submitted', `time_submitted`=@now, `time_updated`=@now, `customer_note`=@note WHERE `id`=@id AND `user_id`=@user (NOT executed)",
                "Line-count gate (COUNT shop_quote_items >= 1) remains PHP-only in this dry-run slice"
            ],
            Detail: "Owned draft quote found in digest window; submit UPDATE simulated. Item lines stay PHP until dual-sample.",
            PhpAjax: "/content/shop/order_process/ajax_quote_submit.php");
    }

    private static StorefrontQuoteSubmitDryRunResult Refuse(
        string status, string code, string detail, StorefrontQuoteSubmitRequest request) =>
        new(status, 0, true, false, true, code, false, request.QuoteId, 0, null,
            request.CustomerNote, [], detail,
            "/content/shop/order_process/ajax_quote_submit.php");
}

public sealed record StorefrontQuoteSubmitRequest(
    long QuoteId,
    string? CustomerNote = null,
    bool ConfirmWrites = false);

public sealed record StorefrontQuoteSubmitDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long QuoteId, int UserId, string? QuoteStatus,
    string? CustomerNote, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { quote_id = QuoteId, customer_note = CustomerNote },
        current = QuoteStatus is null ? null : new { status = QuoteStatus, user_id = UserId },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
