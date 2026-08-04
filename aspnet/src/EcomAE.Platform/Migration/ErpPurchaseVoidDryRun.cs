namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>purchase_void</c>.
/// Simulates soft-void UPDATE; GL reverse journals stay PHP-authoritative.
/// </summary>
public interface IErpPurchaseVoidDryRun
{
    Task<ErpPurchaseVoidDryRunResult> EvaluateAsync(
        ErpPurchaseVoidRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseVoidDryRun : IErpPurchaseVoidDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpPurchaseVoidDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpPurchaseVoidDryRunResult> EvaluateAsync(
        ErpPurchaseVoidRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_void is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseId must be positive.", request);
        }

        var list = await _dashboards.ListErpPurchasesAsync(200, cancellationToken);
        return EvaluateAgainstPurchases(list.Purchases, request);
    }

    public static ErpPurchaseVoidDryRunResult EvaluateAgainstPurchases(
        IReadOnlyList<ErpPurchaseDigest> purchases,
        ErpPurchaseVoidRequest request)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_void is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseId must be positive.", request);
        }

        var purchase = purchases.FirstOrDefault(p => p.Id == request.PurchaseId);
        if (purchase is null)
        {
            return Refuse("dry-run-invalid", "purchase_not_in_digest_window",
                $"Purchase {request.PurchaseId} not present in recent /erp/purchases digest window.",
                request);
        }

        var status = (purchase.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status is "voided" or "cancelled" or "canceled")
        {
            return Refuse("dry-run-invalid", "purchase_already_voided",
                $"Purchase {request.PurchaseId} status '{purchase.Status}' cannot be voided (PHP epc_erp_doc_can_void).",
                request);
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Voided by operator" : request.Reason.Trim();
        if (reason.Length > 255)
        {
            reason = reason[..255];
        }

        return new ErpPurchaseVoidDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            PurchaseId: purchase.Id,
            InvoiceNumber: purchase.InvoiceNumber,
            TotalAmount: purchase.TotalAmount,
            PurchaseStatus: purchase.Status,
            Reason: reason,
            SimulatedSql:
            [
                "UPDATE `epc_erp_supplier_accounting` SET `active`=0 WHERE `purchase_id`=@id AND `active`=1 (NOT executed)",
                "UPDATE `epc_erp_purchases` SET `active`=0, `status`='voided', `voided_at`=@now, `void_reason`=@reason, `voided_by`=@admin, `reversal_journal_id`=@rev WHERE `id`=@id (NOT executed)",
                "GL reverse journals (epc_erp_doc_reverse_journals) remain PHP-only in this dry-run slice"
            ],
            Detail: "Purchase found in digest window; soft-void UPDATE simulated. GL reverse + can_void edge cases stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_void");
    }

    private static ErpPurchaseVoidDryRunResult Refuse(
        string status, string code, string detail, ErpPurchaseVoidRequest request) =>
        new(status, 0, true, false, true, code, false, request.PurchaseId, null, null, null,
            request.Reason, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_void");
}

public sealed record ErpPurchaseVoidRequest(long PurchaseId, string? Reason = null, bool ConfirmWrites = false);

public sealed record ErpPurchaseVoidDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PurchaseId, string? InvoiceNumber, decimal? TotalAmount,
    string? PurchaseStatus, string? Reason, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { purchase_id = PurchaseId, reason = Reason },
        current = InvoiceNumber is null ? null : new { invoice_number = InvoiceNumber, total_amount = TotalAmount, status = PurchaseStatus },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
