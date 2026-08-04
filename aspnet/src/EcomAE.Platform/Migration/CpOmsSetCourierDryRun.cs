namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP OMS <c>ajax_epc_orders_oms.php</c> action <c>set_courier</c>.
/// Simulates courier fee update path; VAT calc + how_get_json stay PHP.
/// Never executes writes. PHP remains authoritative.
/// </summary>
public interface ICpOmsSetCourierDryRun
{
    Task<CpOmsSetCourierDryRunResult> EvaluateAsync(
        CpOmsSetCourierRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsSetCourierDryRun : ICpOmsSetCourierDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsSetCourierDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsSetCourierDryRunResult> EvaluateAsync(
        CpOmsSetCourierRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set_courier is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        if (request.DeliveryPrice < 0)
        {
            return Refuse("dry-run-invalid", "negative_courier_fee",
                "Courier fee cannot be negative (PHP epc_oms_fail).", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request);
    }

    public static CpOmsSetCourierDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsSetCourierRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set_courier is not implemented; PHP ajax_epc_orders_oms.php remains authoritative.",
                request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        if (request.DeliveryPrice < 0)
        {
            return Refuse("dry-run-invalid", "negative_courier_fee",
                "Courier fee cannot be negative (PHP epc_oms_fail).", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order {request.OrderId} not present in recent /cp/orders digest window — expand sample or use PHP OMS console.",
                request);
        }

        if (order.Paid != 0)
        {
            return Refuse("dry-run-invalid", "order_already_paid",
                "Cannot change courier on a paid order (PHP epc_oms_fail).", request);
        }

        var country = string.IsNullOrWhiteSpace(request.Country)
            ? null
            : request.Country.Trim().ToUpperInvariant();
        if (country is { Length: > 2 })
        {
            country = country[..2];
        }

        return new CpOmsSetCourierDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: request.OrderId,
            DeliveryPrice: Math.Round(request.DeliveryPrice, 2),
            Country: country,
            OrderStatus: order.Status,
            OrderPaid: order.Paid,
            SimulatedSql:
            [
                "epc_order_set_courier_charge(@orderId, @fee, country) → UPDATE shop_orders.how_get_json (NOT executed)",
                "INSERT INTO `shop_orders_logs` (…) OMS set courier fee (NOT executed)",
                "VAT destination calc (epc_order_courier_vat_amounts) remains PHP-only in this dry-run slice"
            ],
            Detail: "Unpaid order found in digest window; courier fee update simulated. how_get_json + VAT stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=set_courier");
    }

    private static CpOmsSetCourierDryRunResult Refuse(
        string status, string code, string detail, CpOmsSetCourierRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, request.DeliveryPrice,
            request.Country, null, null, [], detail,
            "/CP/content/shop/order_process/ajax_epc_orders_oms.php?action=set_courier");
}

public sealed record CpOmsSetCourierRequest(
    long OrderId,
    decimal DeliveryPrice,
    string? Country = null,
    bool ConfirmWrites = false);

public sealed record CpOmsSetCourierDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, decimal DeliveryPrice, string? Country,
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
        intended = new { order_id = OrderId, delivery_price = DeliveryPrice, country = Country },
        order_context = OrderStatus is null ? null : new { status = OrderStatus, paid = OrderPaid },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
