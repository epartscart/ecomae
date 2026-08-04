namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_add_comment_to_log.php</c> (OMS order log INSERT).
/// Never executes writes. PHP remains authoritative.
/// </summary>
public interface ICpOmsAddCommentDryRun
{
    Task<CpOmsAddCommentDryRunResult> EvaluateAsync(
        CpOmsAddCommentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsAddCommentDryRun : ICpOmsAddCommentDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsAddCommentDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsAddCommentDryRunResult> EvaluateAsync(
        CpOmsAddCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS add-comment is not implemented; PHP ajax_add_comment_to_log.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Refuse("dry-run-invalid", "comment_text_required",
                "Comment text is required.", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request with { Text = text });
    }

    public static CpOmsAddCommentDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsAddCommentRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS add-comment is not implemented; PHP ajax_add_comment_to_log.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Refuse("dry-run-invalid", "comment_text_required",
                "Comment text is required.", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders digest window — expand sample or use PHP OMS console.",
                request);
        }

        var preview = text.Length > 200 ? text[..200] + "…" : text;

        return new CpOmsAddCommentDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            TextPreview: preview,
            OrderStatus: order.Status,
            OrderPaid: order.Paid,
            SimulatedSql:
            [
                "INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (@orderId,@now,@adminId,1,@text) (NOT executed)",
                "CSRF / admin ACL remain PHP-only in this dry-run slice"
            ],
            Detail: "Order found in digest window; manager log INSERT simulated.",
            PhpAjax: "/CP/content/shop/order_process/ajax_add_comment_to_log.php");
    }

    private static CpOmsAddCommentDryRunResult Refuse(
        string status, string code, string detail, CpOmsAddCommentRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId,
            request.Text, null, null, [], detail,
            "/CP/content/shop/order_process/ajax_add_comment_to_log.php");
}

public sealed record CpOmsAddCommentRequest(
    long OrderId,
    string? Text,
    bool ConfirmWrites = false);

public sealed record CpOmsAddCommentDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, string? TextPreview,
    int? OrderStatus, int? OrderPaid, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "cp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { order_id = OrderId, text = TextPreview },
        current = OrderStatus is null ? null : new { status = OrderStatus, paid = OrderPaid },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
