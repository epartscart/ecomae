namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>po_delete</c> (draft POs only).
/// Never executes DELETE. PHP remains authoritative.
/// </summary>
public interface IErpPoDeleteDryRun
{
    Task<ErpPoDeleteDryRunResult> EvaluateAsync(
        ErpPoDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPoDeleteDryRun : IErpPoDeleteDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpPoDeleteDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpPoDeleteDryRunResult> EvaluateAsync(
        ErpPoDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET po_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseOrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseOrderId must be positive.", request);
        }

        var list = await _dashboards.ListErpPurchaseOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(list.Orders, request);
    }

    public static ErpPoDeleteDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<ErpPurchaseOrderDigest> orders,
        ErpPoDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET po_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseOrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseOrderId must be positive.", request);
        }

        var po = orders.FirstOrDefault(o => o.Id == request.PurchaseOrderId);
        if (po is null)
        {
            return Refuse("dry-run-invalid", "po_not_in_digest_window",
                $"PO {request.PurchaseOrderId} not present in recent /erp/purchase-orders digest window.",
                request);
        }

        var status = (po.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is not ("draft" or "" or "new"))
        {
            return Refuse("dry-run-invalid", "po_not_draft",
                $"Only draft purchase orders can be deleted — status '{po.Status}' (PHP epc_erp_doc_can_delete).",
                request);
        }

        return new ErpPoDeleteDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            PurchaseOrderId: po.Id,
            PoNo: po.PoNo,
            OrderStatus: po.Status,
            TotalAmount: po.TotalAmount,
            SimulatedSql:
            [
                "DELETE FROM `epc_erp_po_lines` WHERE `po_id`=@id (NOT executed)",
                "DELETE FROM `epc_erp_po_receipts` WHERE `po_id`=@id (NOT executed)",
                "DELETE FROM `epc_erp_purchase_orders` WHERE `id`=@id (NOT executed)"
            ],
            Detail: "Draft PO found in digest window; hard-delete simulated. can_delete edge cases stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=po_delete");
    }

    private static ErpPoDeleteDryRunResult Refuse(
        string status, string code, string detail, ErpPoDeleteRequest request) =>
        new(status, 0, true, false, true, code, false, request.PurchaseOrderId, null, null, null,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=po_delete");
}

public sealed record ErpPoDeleteRequest(long PurchaseOrderId, bool ConfirmWrites = false);

public sealed record ErpPoDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PurchaseOrderId, string? PoNo, string? OrderStatus,
    decimal? TotalAmount, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { purchase_order_id = PurchaseOrderId },
        current = PoNo is null ? null : new { po_no = PoNo, status = OrderStatus, total_amount = TotalAmount },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
