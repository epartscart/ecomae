namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>so_delete</c> (draft only). Never DELETE. PHP authoritative.
/// </summary>
public interface IErpSalesOrderDeleteDryRun
{
    Task<ErpSalesOrderDeleteDryRunResult> EvaluateAsync(
        ErpSalesOrderDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpSalesOrderDeleteDryRun : IErpSalesOrderDeleteDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpSalesOrderDeleteDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpSalesOrderDeleteDryRunResult> EvaluateAsync(
        ErpSalesOrderDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET so_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.SalesOrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "salesOrderId must be positive.", request);
        }

        var list = await _dashboards.ListErpSalesOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(list.Orders, request);
    }

    public static ErpSalesOrderDeleteDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<ErpSalesOrderDigest> orders,
        ErpSalesOrderDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET so_delete is not implemented; PHP ajax_erp.php remains authoritative.",
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
        if (status is not "draft")
        {
            return Refuse("dry-run-invalid", "not_draft",
                "Only draft sales orders can be deleted (PHP epc_erp_sales_order_delete).",
                request);
        }

        return new ErpSalesOrderDeleteDryRunResult(
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
            SimulatedSql:
            [
                "DELETE FROM `epc_erp_sales_order_lines` WHERE sales_order_id=@id (NOT executed)",
                "DELETE FROM `epc_erp_sales_orders` WHERE id=@id (NOT executed)"
            ],
            Detail: "Draft SO found; hard-delete simulated. Confirmed/invoiced must use so_cancel (PHP).",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=so_delete");
    }

    private static ErpSalesOrderDeleteDryRunResult Refuse(
        string status, string code, string detail, ErpSalesOrderDeleteRequest request) =>
        new(status, 0, true, false, true, code, false, request.SalesOrderId, null, null, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=so_delete");
}

public sealed record ErpSalesOrderDeleteRequest(long SalesOrderId, bool ConfirmWrites = false);

public sealed record ErpSalesOrderDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long SalesOrderId, string? SoNo, string? OrderStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { sales_order_id = SalesOrderId },
        current = SoNo is null ? null : new { so_no = SoNo, status = OrderStatus },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
