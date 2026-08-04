namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>so_cancel</c>.
/// Simulates status → cancelled; invoiced SOs refused. PHP remains authoritative.
/// </summary>
public interface IErpSalesOrderCancelDryRun
{
    Task<ErpSalesOrderCancelDryRunResult> EvaluateAsync(
        ErpSalesOrderCancelRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpSalesOrderCancelDryRun : IErpSalesOrderCancelDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpSalesOrderCancelDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpSalesOrderCancelDryRunResult> EvaluateAsync(
        ErpSalesOrderCancelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET so_cancel is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.SalesOrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "salesOrderId must be positive.", request);
        }

        var list = await _dashboards.ListErpSalesOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(list.Orders, request);
    }

    public static ErpSalesOrderCancelDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<ErpSalesOrderDigest> orders,
        ErpSalesOrderCancelRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET so_cancel is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.SalesOrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "salesOrderId must be positive.", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.SalesOrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "so_not_in_digest_window",
                $"Sales order {request.SalesOrderId} not present in recent /erp/sales-orders digest window.",
                request);
        }

        var status = (order.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is "invoiced")
        {
            return Refuse("dry-run-invalid", "so_already_invoiced",
                "Invoiced sales orders cannot be cancelled — issue a credit note on the invoice (PHP).",
                request);
        }

        if (status is "cancelled" or "canceled")
        {
            return Refuse("dry-run-invalid", "so_already_cancelled",
                $"Sales order {request.SalesOrderId} is already cancelled.",
                request);
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is { Length: > 255 })
        {
            reason = reason[..255];
        }

        return new ErpSalesOrderCancelDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            SalesOrderId: order.Id,
            SoNo: order.SoNo,
            OrderStatus: order.Status,
            TotalAmount: order.TotalAmount,
            Reason: reason,
            SimulatedSql:
            [
                "epc_erp_sales_order_set_status(@id, 'cancelled') (NOT executed)",
                "Audit log cancel remains PHP-only when reason provided"
            ],
            Detail: "SO found in digest window and not invoiced; cancel simulated. sales_invoice_id edge cases stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=so_cancel");
    }

    private static ErpSalesOrderCancelDryRunResult Refuse(
        string status, string code, string detail, ErpSalesOrderCancelRequest request) =>
        new(status, 0, true, false, true, code, false, request.SalesOrderId, null, null, null,
            request.Reason, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=so_cancel");
}

public sealed record ErpSalesOrderCancelRequest(
    long SalesOrderId,
    string? Reason = null,
    bool ConfirmWrites = false);

public sealed record ErpSalesOrderCancelDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long SalesOrderId, string? SoNo, string? OrderStatus,
    decimal? TotalAmount, string? Reason, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { sales_order_id = SalesOrderId, reason = Reason },
        current = SoNo is null ? null : new { so_no = SoNo, status = OrderStatus, total_amount = TotalAmount },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
