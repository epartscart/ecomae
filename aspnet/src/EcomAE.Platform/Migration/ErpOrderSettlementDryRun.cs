namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>order_settlement</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpOrderSettlementDryRun
{
    Task<ErpOrderSettlementDryRunResult> EvaluateAsync(
        ErpOrderSettlementRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpOrderSettlementDryRun : IErpOrderSettlementDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;
    public ErpOrderSettlementDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpOrderSettlementDryRunResult> EvaluateAsync(
        ErpOrderSettlementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET order_settlement is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and positive amount required (PHP).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static ErpOrderSettlementDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders, ErpOrderSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET order_settlement is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "orderId and positive amount required (PHP).", request);
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

        var direction = (request.Direction ?? "credit").Trim().ToLowerInvariant();
        if (direction is not ("credit" or "debit"))
        {
            return Refuse("dry-run-invalid", "invalid_direction",
                "Direction must be credit or debit.", request);
        }

        return new ErpOrderSettlementDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            request.OrderId, order.UserId, request.Amount, direction,
            [
                "Resolve user_id from shop_orders (NOT executed)",
                "epc_erp_customer_settlement INSERT shop_users_accounting (NOT executed)"
            ],
            "Order found; revenue settlement via customer_settlement simulated.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=order_settlement");
    }

    private static ErpOrderSettlementDryRunResult Refuse(
        string status, string code, string detail, ErpOrderSettlementRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, 0, request.Amount, request.Direction, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=order_settlement");
}

public sealed record ErpOrderSettlementRequest(
    long OrderId, decimal Amount, string? Direction = "credit", bool ConfirmWrites = false);

public sealed record ErpOrderSettlementDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, int UserId, decimal Amount, string? Direction,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { order_id = OrderId, user_id = UserId, amount = Amount, direction = Direction },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
