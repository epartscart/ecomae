namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_order_pay_refund.php</c> (seller refund reflection).
/// Never executes accounting INSERT/UPDATE. PHP remains authoritative.
/// </summary>
public interface ICpOmsPayRefundDryRun
{
    Task<CpOmsPayRefundDryRunResult> EvaluateAsync(
        CpOmsPayRefundRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsPayRefundDryRun : ICpOmsPayRefundDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsPayRefundDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsPayRefundDryRunResult> EvaluateAsync(
        CpOmsPayRefundRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS pay-refund is not implemented; PHP ajax_order_pay_refund.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId must be positive (PHP Forbidden without order_id).", request);
        }

        // PHP requires direct_refund to be present; accept bool.
        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsPayRefundDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsPayRefundRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS pay-refund is not implemented; PHP ajax_order_pay_refund.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId must be positive (PHP Forbidden without order_id).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.", request);
        }

        return new CpOmsPayRefundDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            DirectRefund: request.DirectRefund,
            PaidSumHint: request.PaidSum,
            SimulatedSql:
            [
                "SELECT paid_sum / customer_balance from shop_users_accounting (NOT executed)",
                "INSERT INTO `shop_users_accounting` refund row (NOT executed)",
                "UPDATE order paid flags / notify customer (NOT executed)"
            ],
            Detail: "Order found in digest window; paid_sum/balance arithmetic + accounting INSERT stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_order_pay_refund.php");
    }

    private static CpOmsPayRefundDryRunResult Refuse(
        string status, string code, string detail, CpOmsPayRefundRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, request.DirectRefund,
            request.PaidSum, [], detail, "/CP/content/shop/order_process/ajax_order_pay_refund.php");
}

public sealed record CpOmsPayRefundRequest(
    long OrderId,
    bool DirectRefund,
    decimal? PaidSum = null,
    bool ConfirmWrites = false);

public sealed record CpOmsPayRefundDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, bool DirectRefund, decimal? PaidSumHint,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { order_id = OrderId, direct_refund = DirectRefund, paid_sum = PaidSumHint },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
