namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>ajax_epc_orders_oms.php</c> action <c>set_item_status</c>.
/// Never executes UPDATE/INSERT. PHP remains authoritative.
/// </summary>
public interface ICpOmsSetItemStatusDryRun
{
    Task<CpOmsSetItemStatusDryRunResult> EvaluateAsync(
        CpOmsSetItemStatusRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsSetItemStatusDryRun : ICpOmsSetItemStatusDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsSetItemStatusDryRun(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<CpOmsSetItemStatusDryRunResult> EvaluateAsync(
        CpOmsSetItemStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set_item_status is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.ItemId <= 0 || request.Status <= 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "orderId, itemId, and status must be positive (PHP Invalid item status).",
                request);
        }

        // Order-level existence from recent OMS digest window (item row verified only on PHP live path for now).
        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsSetItemStatusDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsSetItemStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set_item_status is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.ItemId <= 0 || request.Status <= 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "orderId, itemId, and status must be positive (PHP Invalid item status).",
                request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse(
                "dry-run-invalid",
                "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window — expand sample or use PHP OMS console.",
                request);
        }

        return new CpOmsSetItemStatusDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            ItemId: request.ItemId,
            StatusId: request.Status,
            OrderStatus: order.Status,
            OrderPaid: order.Paid,
            SimulatedSql:
            [
                "UPDATE `shop_orders_items` SET `status` = @status WHERE `id` = @itemId AND `order_id` = @orderId (NOT executed)",
                "INSERT INTO `shop_orders_logs` (…) OMS set item status (NOT executed)"
            ],
            Detail: "Order found in digest window; item-row existence + notifications stay PHP-authoritative until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=set_item_status");
    }

    private static CpOmsSetItemStatusDryRunResult Refuse(
        string status,
        string validationCode,
        string detail,
        CpOmsSetItemStatusRequest request) =>
        new(
            Status: status,
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: false,
            OrderId: request.OrderId,
            ItemId: request.ItemId,
            StatusId: request.Status,
            OrderStatus: null,
            OrderPaid: null,
            SimulatedSql: [],
            Detail: detail,
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=set_item_status");
}

public sealed record CpOmsSetItemStatusRequest(long OrderId, long ItemId, int Status, bool ConfirmWrites = false);

public sealed record CpOmsSetItemStatusDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long OrderId,
    long ItemId,
    int StatusId,
    int? OrderStatus,
    int? OrderPaid,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax)
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
        intended = new
        {
            order_id = OrderId,
            item_id = ItemId,
            status = StatusId
        },
        order_context = OrderStatus is null ? null : new { status = OrderStatus, paid = OrderPaid },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
