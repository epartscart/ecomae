namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>purchase_adjustment</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPurchaseAdjustmentDryRun
{
    Task<ErpPurchaseAdjustmentDryRunResult> EvaluateAsync(
        ErpPurchaseAdjustmentRequest request, CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseAdjustmentDryRun : IErpPurchaseAdjustmentDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;
    public ErpPurchaseAdjustmentDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpPurchaseAdjustmentDryRunResult> EvaluateAsync(
        ErpPurchaseAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_adjustment is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseId <= 0 || Math.Abs(request.DeltaExVat) < 0.01m)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Purchase ID and non-zero adjustment amount required (PHP).", request);
        }

        var list = await _dashboards.ListErpPurchasesAsync(200, cancellationToken);
        return EvaluateAgainstPurchases(list.Purchases, request);
    }

    public static ErpPurchaseAdjustmentDryRunResult EvaluateAgainstPurchases(
        IReadOnlyList<ErpPurchaseDigest> purchases, ErpPurchaseAdjustmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(purchases);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET purchase_adjustment is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.PurchaseId <= 0 || Math.Abs(request.DeltaExVat) < 0.01m)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Purchase ID and non-zero adjustment amount required (PHP).", request);
        }

        var row = purchases.FirstOrDefault(p => p.Id == request.PurchaseId);
        if (row is null)
        {
            return Refuse("dry-run-invalid", "purchase_not_in_digest_window",
                $"Purchase {request.PurchaseId} not in recent digest window.", request);
        }

        return new ErpPurchaseAdjustmentDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            row.Id, request.DeltaExVat, request.Note,
            [
                "Tax toolkit VAT recalc on new amount_ex_vat (NOT executed)",
                "UPDATE `epc_erp_purchases` amounts + note append (NOT executed)",
                "epc_erp_supplier_settlement for delta total (NOT executed)"
            ],
            "Purchase found; cost adjustment + supplier settlement simulated. Negative-total edge stays PHP.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_adjustment");
    }

    private static ErpPurchaseAdjustmentDryRunResult Refuse(
        string status, string code, string detail, ErpPurchaseAdjustmentRequest request) =>
        new(status, 0, true, false, true, code, false, request.PurchaseId, request.DeltaExVat, request.Note, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=purchase_adjustment");
}

public sealed record ErpPurchaseAdjustmentRequest(
    long PurchaseId, decimal DeltaExVat, string? Note = null, bool ConfirmWrites = false);

public sealed record ErpPurchaseAdjustmentDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PurchaseId, decimal DeltaExVat, string? Note,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { purchase_id = PurchaseId, delta_ex_vat = DeltaExVat, note = Note },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
