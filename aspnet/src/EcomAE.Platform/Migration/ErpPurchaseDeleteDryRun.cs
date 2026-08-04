namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>purchase_delete</c> (draft only). Never DELETE. PHP authoritative.</summary>
public interface IErpPurchaseDeleteDryRun
{
    Task<ErpPurchaseDeleteDryRunResult> EvaluateAsync(ErpPurchaseDeleteRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseDeleteDryRun : IErpPurchaseDeleteDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;
    public ErpPurchaseDeleteDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpPurchaseDeleteDryRunResult> EvaluateAsync(
        ErpPurchaseDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseId must be positive.", request);
        }

        var list = await _dashboards.ListErpPurchasesAsync(200, cancellationToken);
        return EvaluateAgainstPurchases(list.Purchases, request);
    }

    public static ErpPurchaseDeleteDryRunResult EvaluateAgainstPurchases(
        IReadOnlyList<ErpPurchaseDigest> purchases, ErpPurchaseDeleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_delete is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseId must be positive.", request);
        }

        var row = purchases.FirstOrDefault(p => p.Id == request.PurchaseId);
        if (row is null)
        {
            return Refuse("dry-run-invalid", "purchase_not_in_digest_window",
                $"Purchase {request.PurchaseId} not in recent digest window.", request);
        }

        var status = (row.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is not ("draft" or "" or "new"))
        {
            return Refuse("dry-run-invalid", "not_draft",
                $"Only draft unposted purchases can be deleted — status '{row.Status}' (PHP epc_erp_doc_can_delete).",
                request);
        }

        return new ErpPurchaseDeleteDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, row.Id, row.Status,
            [
                "DELETE FROM `epc_erp_supplier_accounting` WHERE purchase_id=@id (NOT executed)",
                "DELETE FROM `epc_erp_purchases` WHERE id=@id (NOT executed)"
            ],
            "Draft purchase found; hard-delete simulated. Posted invoices must use void (PHP).",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_delete");
    }

    private static ErpPurchaseDeleteDryRunResult Refuse(
        string status, string code, string detail, ErpPurchaseDeleteRequest request) =>
        new(status, 0, true, false, true, code, false, request.PurchaseId, null, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_delete");
}

public sealed record ErpPurchaseDeleteRequest(long PurchaseId, bool ConfirmWrites = false);

public sealed record ErpPurchaseDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PurchaseId, string? OrderStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { purchase_id = PurchaseId },
        current = OrderStatus is null ? null : new { status = OrderStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
