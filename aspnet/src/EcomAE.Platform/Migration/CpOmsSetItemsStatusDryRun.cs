namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>ajax_epc_orders_oms.php</c> action <c>set_items_status</c> (bulk).
/// Never executes UPDATE. PHP remains authoritative.
/// </summary>
public interface ICpOmsSetItemsStatusDryRun
{
    Task<CpOmsSetItemsStatusDryRunResult> EvaluateAsync(
        CpOmsSetItemsStatusRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsSetItemsStatusDryRun : ICpOmsSetItemsStatusDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsSetItemsStatusDryRun(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<CpOmsSetItemsStatusDryRunResult> EvaluateAsync(
        CpOmsSetItemsStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set_items_status is not implemented; PHP remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.Status <= 0 || request.ItemIds.Count == 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "orderId, status, and itemIds[] must be provided (PHP Invalid status / empty ids).",
                request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsSetItemsStatusDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsSetItemsStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set_items_status is not implemented; PHP remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.Status <= 0 || request.ItemIds.Count == 0 || request.ItemIds.Any(id => id <= 0))
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "orderId, status, and positive itemIds[] required.",
                request);
        }

        if (orders.All(o => o.Id != request.OrderId))
        {
            return Refuse(
                "dry-run-invalid",
                "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.",
                request);
        }

        var ids = request.ItemIds.Distinct().ToArray();
        return new CpOmsSetItemsStatusDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            StatusId: request.Status,
            ItemIds: ids,
            SimulatedSql: $"UPDATE `shop_orders_items` SET `status` = @status WHERE `id` IN ({string.Join(",", ids)}) AND `order_id` = @orderId (NOT executed)",
            Detail: $"Would set status={request.Status} on {ids.Length} line(s); write blocked until dual-sample + approval.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=set_items_status");
    }

    private static CpOmsSetItemsStatusDryRunResult Refuse(
        string status,
        string validationCode,
        string detail,
        CpOmsSetItemsStatusRequest request) =>
        new(
            Status: status,
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: false,
            OrderId: request.OrderId,
            StatusId: request.Status,
            ItemIds: request.ItemIds,
            SimulatedSql: null,
            Detail: detail,
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=set_items_status");
}

public sealed record CpOmsSetItemsStatusRequest(long OrderId, int Status, IReadOnlyList<long> ItemIds, bool ConfirmWrites = false);

public sealed record CpOmsSetItemsStatusDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long OrderId,
    int StatusId,
    IReadOnlyList<long> ItemIds,
    string? SimulatedSql,
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
        intended = new { order_id = OrderId, status = StatusId, item_ids = ItemIds },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
