namespace EcomAE.Platform.Migration;

/// <summary>
/// Validation envelope for the live ASP.NET purchase-amend writer.
/// </summary>
public interface IErpPurchaseAmendDryRun
{
    Task<ErpPurchaseAmendDryRunResult> EvaluateAsync(
        ErpPurchaseAmendRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseAmendDryRun : IErpPurchaseAmendDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpPurchaseAmendDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpPurchaseAmendDryRunResult> EvaluateAsync(
        ErpPurchaseAmendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "Use confirm_writes=true to execute the live ASP.NET purchase-amend writer.",
                request);
        }

        if (request.PurchaseId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "purchaseId must be positive.", request);
        }

        var list = await _dashboards.ListErpPurchasesAsync(200, cancellationToken);
        return EvaluateAgainstPurchases(list.Purchases, request);
    }

    public static ErpPurchaseAmendDryRunResult EvaluateAgainstPurchases(
        IReadOnlyList<ErpPurchaseDigest> purchases,
        ErpPurchaseAmendRequest request)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "Use confirm_writes=true to execute the live ASP.NET purchase-amend writer.",
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
        if (status is "void" or "voided" or "cancelled" or "canceled")
        {
            return Refuse("dry-run-invalid", "cannot_amend",
                $"Purchase cannot be amended — status '{row.Status}' (PHP).", request);
        }

        var note = request.Note?.Trim();
        var invNo = request.InvoiceNumber?.Trim();
        var isDraft = status is "draft" or "" or "new";
        var amountPath = isDraft && request.AmountExVat is > 0;

        return new ErpPurchaseAmendDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: false,
            ValidationCode: "ok",
            WouldWrite: true,
            PurchaseId: row.Id,
            OrderStatus: row.Status,
            InvoiceNumber: invNo ?? row.InvoiceNumber,
            Note: note,
            AmountExVat: amountPath ? request.AmountExVat : null,
            SimulatedSql: amountPath
                ?
                [
                    "Tax toolkit VAT recalc for draft amount_ex_vat (NOT executed)",
                    "UPDATE `epc_erp_purchases` SET invoice_number/amount/vat/total/note WHERE id=@id AND status='draft' (NOT executed)",
                    "epc_erp_audit_log amend purchase (NOT executed)"
                ]
                :
                [
                    "UPDATE `epc_erp_purchases` SET `invoice_number`=@inv, `note`=@note WHERE `id`=@id AND `active`=1 (NOT executed)",
                    "epc_erp_audit_log amend purchase (NOT executed)",
                    "Posted amount edits stay PHP-refused — narrative-only path simulated"
                ],
            Detail: amountPath
                ? "Draft purchase found; full amount amend + VAT recalc simulated."
                : "Purchase found; invoice_number/note amend validated and no write was performed. Voided/inactive edge cases remain rejected.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_amend");
    }

    private static ErpPurchaseAmendDryRunResult Refuse(
        string status, string code, string detail, ErpPurchaseAmendRequest request) =>
        new(status, 0, true, false, false, code, false, request.PurchaseId, null,
            request.InvoiceNumber, request.Note, request.AmountExVat, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_amend");
}

public sealed record ErpPurchaseAmendRequest(
    long PurchaseId,
    string? InvoiceNumber = null,
    string? Note = null,
    decimal? AmountExVat = null,
    bool ConfirmWrites = false);

public sealed record ErpPurchaseAmendDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PurchaseId, string? OrderStatus,
    string? InvoiceNumber, string? Note, decimal? AmountExVat,
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
        intended = new
        {
            purchase_id = PurchaseId,
            invoice_number = InvoiceNumber,
            note = Note,
            amount_ex_vat = AmountExVat
        },
        current = OrderStatus is null ? null : new { status = OrderStatus },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
