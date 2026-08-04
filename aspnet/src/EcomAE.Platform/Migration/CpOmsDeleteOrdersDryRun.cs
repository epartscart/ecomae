namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_delete_orders.php</c>.
/// Only unpaid orders (paid==0) in digest window; never executes DELETE.
/// </summary>
public interface ICpOmsDeleteOrdersDryRun
{
    Task<CpOmsDeleteOrdersDryRunResult> EvaluateAsync(
        CpOmsDeleteOrdersRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsDeleteOrdersDryRun : ICpOmsDeleteOrdersDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsDeleteOrdersDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsDeleteOrdersDryRunResult> EvaluateAsync(
        CpOmsDeleteOrdersRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET order delete is not implemented; PHP ajax_delete_orders.php remains authoritative.",
                request);
        }

        var ids = (request.OrderIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orders_list must contain at least one positive orderId.", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request with { OrderIds = ids });
    }

    public static CpOmsDeleteOrdersDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsDeleteOrdersRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET order delete is not implemented; PHP ajax_delete_orders.php remains authoritative.",
                request);
        }

        var ids = (request.OrderIds ?? []).Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orders_list must contain at least one positive orderId.", request);
        }

        var missing = ids.Where(id => orders.All(o => o.Id != id)).ToList();
        if (missing.Count > 0)
        {
            return Refuse("dry-run-invalid", "orders_not_in_digest_window",
                $"Order(s) {string.Join(",", missing)} not in recent /cp/orders digest window.",
                request with { OrderIds = ids });
        }

        var paid = ids.Where(id => orders.First(o => o.Id == id).Paid != 0).ToList();
        if (paid.Count > 0)
        {
            return Refuse("dry-run-invalid", "orders_paid",
                $"Cannot delete paid/partially-paid order(s) {string.Join(",", paid)} (PHP paid!=0 gate).",
                request with { OrderIds = ids });
        }

        return new CpOmsDeleteOrdersDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderIds: ids,
            SimulatedSql:
            [
                "DELETE FROM `shop_orders` WHERE `id` IN (…) (NOT executed)",
                "DELETE FROM `shop_orders_items` / `_details` / `_logs` / `_messages` / `_viewed` (NOT executed)"
            ],
            Detail: "All target orders unpaid and in digest window; multi-table DELETE simulated. Transaction stays PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_delete_orders.php");
    }

    private static CpOmsDeleteOrdersDryRunResult Refuse(
        string status, string code, string detail, CpOmsDeleteOrdersRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderIds ?? [], [], detail,
            "/CP/content/shop/order_process/ajax_delete_orders.php");
}

public sealed record CpOmsDeleteOrdersRequest(IReadOnlyList<long> OrderIds, bool ConfirmWrites = false);

public sealed record CpOmsDeleteOrdersDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, IReadOnlyList<long> OrderIds,
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
        intended = new { order_ids = OrderIds },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
