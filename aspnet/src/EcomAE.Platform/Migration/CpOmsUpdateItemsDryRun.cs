namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP OMS <c>update_items</c> (bulk). Never UPDATE. PHP authoritative.</summary>
public interface ICpOmsUpdateItemsDryRun
{
    Task<CpOmsUpdateItemsDryRunResult> EvaluateAsync(CpOmsUpdateItemsRequest request, CancellationToken cancellationToken = default);
}

public sealed class CpOmsUpdateItemsDryRun : ICpOmsUpdateItemsDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;
    public CpOmsUpdateItemsDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsUpdateItemsDryRunResult> EvaluateAsync(
        CpOmsUpdateItemsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS update_items is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Refuse("dry-run-invalid", "no_items", "No items to update (PHP).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsUpdateItemsDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders, CpOmsUpdateItemsRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS update_items is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Refuse("dry-run-invalid", "no_items", "No items to update (PHP).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not in recent /cp/orders-digest window.", request);
        }

        var valid = request.Items.Count(i => i.ItemId > 0);
        return new CpOmsUpdateItemsDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, request.OrderId, valid,
            [
                $"UPDATE `shop_orders_items` for up to {valid} item(s) (NOT executed)",
                "Warehouse reprice / OMS log (NOT executed)"
            ],
            "Order found; bulk item UPDATE blocked. Per-row brand/qty/price edge cases stay PHP until dual-sample.",
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=update_items");
    }

    private static CpOmsUpdateItemsDryRunResult Refuse(
        string status, string code, string detail, CpOmsUpdateItemsRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, 0, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=update_items");
}

public sealed record CpOmsUpdateItemsItem(long ItemId, decimal? Price = null, int? CountNeed = null);
public sealed record CpOmsUpdateItemsRequest(long OrderId, IReadOnlyList<CpOmsUpdateItemsItem>? Items, bool ConfirmWrites = false);

public sealed record CpOmsUpdateItemsDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, int ItemHint,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "cp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { order_id = OrderId, item_hint = ItemHint },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
