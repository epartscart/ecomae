namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>refresh_item_cost</c>.
/// Never executes UPDATE. Warehouse price lookup stays PHP. PHP authoritative.
/// </summary>
public interface ICpOmsRefreshItemCostDryRun
{
    Task<CpOmsRefreshItemCostDryRunResult> EvaluateAsync(
        CpOmsRefreshItemCostRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsRefreshItemCostDryRun : ICpOmsRefreshItemCostDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsRefreshItemCostDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsRefreshItemCostDryRunResult> EvaluateAsync(
        CpOmsRefreshItemCostRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS refresh_item_cost is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.ItemId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and itemId must be positive (PHP Invalid item).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsRefreshItemCostDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsRefreshItemCostRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS refresh_item_cost is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.ItemId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and itemId must be positive (PHP Invalid item).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.", request);
        }

        return new CpOmsRefreshItemCostDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            ItemId: request.ItemId,
            SimulatedSql:
            [
                "SELECT item row + warehouse offer / effective purchase (NOT executed)",
                "UPDATE `shop_orders_items` SET t2_price_purchase[/price] WHERE id=@item AND order_id=@order (NOT executed)",
                "INSERT OMS log refresh line (NOT executed)"
            ],
            Detail: "Order found; cost refresh UPDATE simulated. Offer lookup + item ownership stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=refresh_item_cost");
    }

    private static CpOmsRefreshItemCostDryRunResult Refuse(
        string status, string code, string detail, CpOmsRefreshItemCostRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, request.ItemId, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=refresh_item_cost");
}

public sealed record CpOmsRefreshItemCostRequest(long OrderId, long ItemId, bool ConfirmWrites = false);

public sealed record CpOmsRefreshItemCostDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, long ItemId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "cp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { order_id = OrderId, item_id = ItemId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
