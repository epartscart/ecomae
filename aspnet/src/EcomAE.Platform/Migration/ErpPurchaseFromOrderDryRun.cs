namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>purchase_from_order</c>. Never INSERT. PHP authoritative.
/// </summary>
public interface IErpPurchaseFromOrderDryRun
{
    Task<ErpPurchaseFromOrderDryRunResult> EvaluateAsync(
        ErpPurchaseFromOrderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseFromOrderDryRun : IErpPurchaseFromOrderDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpPurchaseFromOrderDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpPurchaseFromOrderDryRunResult> EvaluateAsync(
        ErpPurchaseFromOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_from_order is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.SupplierId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and supplierId must be positive.", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static ErpPurchaseFromOrderDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        ErpPurchaseFromOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_from_order is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.SupplierId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and supplierId must be positive.", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders-digest window.", request);
        }

        if (order.SuccessfullyCreated != 1)
        {
            return Refuse("dry-run-invalid", "order_incomplete",
                "Order must be successfully_created=1 (PHP epc_erp_assert_order_complete).", request);
        }

        return new ErpPurchaseFromOrderDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            SupplierId: request.SupplierId,
            SimulatedSql:
            [
                "Compute purchase_ex_vat from shop_orders_items (NOT executed)",
                "INSERT INTO `epc_erp_purchases` from order (NOT executed)",
                "Optional inventory receipt + blockchain GRN flash stay PHP"
            ],
            Detail: "Order found and complete flag OK; purchase-from-order INSERT simulated. Purchase-cost=0 edge stays PHP.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_from_order");
    }

    private static ErpPurchaseFromOrderDryRunResult Refuse(
        string status, string code, string detail, ErpPurchaseFromOrderRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, request.SupplierId, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_from_order");
}

public sealed record ErpPurchaseFromOrderRequest(long OrderId, long SupplierId, bool ConfirmWrites = false);

public sealed record ErpPurchaseFromOrderDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, long SupplierId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { order_id = OrderId, supplier_id = SupplierId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
