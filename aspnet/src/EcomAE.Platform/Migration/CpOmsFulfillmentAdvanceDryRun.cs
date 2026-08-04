namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>supplier_fulfillment_advance</c>.
/// Never executes UPDATE/INSERT. PHP remains authoritative.
/// </summary>
public interface ICpOmsFulfillmentAdvanceDryRun
{
    Task<CpOmsFulfillmentAdvanceDryRunResult> EvaluateAsync(
        CpOmsFulfillmentAdvanceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsFulfillmentAdvanceDryRun : ICpOmsFulfillmentAdvanceDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsFulfillmentAdvanceDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsFulfillmentAdvanceDryRunResult> EvaluateAsync(
        CpOmsFulfillmentAdvanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS fulfillment_advance is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var key = (request.SupplierKey ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return Refuse("dry-run-invalid", "supplier_key_required",
                "supplier_key required (PHP).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request with { SupplierKey = key });
    }

    public static CpOmsFulfillmentAdvanceDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsFulfillmentAdvanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS fulfillment_advance is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var key = (request.SupplierKey ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return Refuse("dry-run-invalid", "supplier_key_required",
                "supplier_key required (PHP).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.", request);
        }

        return new CpOmsFulfillmentAdvanceDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            SupplierKey: key,
            SimulatedSql:
            [
                "epc_order_supplier_fulfillment_bootstrap(@order) (NOT executed)",
                "SELECT stage → next stage via epc_order_supplier_fulfillment_advance (NOT executed)",
                "UPDATE `epc_order_supplier_fulfillment` SET stage=@next WHERE order_id=@order AND supplier_key=@key (NOT executed)",
                "INSERT shop_orders_logs OMS advance line (NOT executed)"
            ],
            Detail: "Order found; advance-to-next-stage simulated. Terminal 'complete' no-op + current stage stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=supplier_fulfillment_advance");
    }

    private static CpOmsFulfillmentAdvanceDryRunResult Refuse(
        string status, string code, string detail, CpOmsFulfillmentAdvanceRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.OrderId, request.SupplierKey, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=supplier_fulfillment_advance");
}

public sealed record CpOmsFulfillmentAdvanceRequest(
    long OrderId,
    string? SupplierKey,
    bool ConfirmWrites = false);

public sealed record CpOmsFulfillmentAdvanceDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, string? SupplierKey,
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
        intended = new { order_id = OrderId, supplier_key = SupplierKey },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
