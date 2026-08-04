namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>create_purchase</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpPurchaseCreateDryRun
{
    ErpPurchaseCreateDryRunResult Evaluate(ErpPurchaseCreateRequest request);
}

public sealed class ErpPurchaseCreateDryRun : IErpPurchaseCreateDryRun
{
    public ErpPurchaseCreateDryRunResult Evaluate(ErpPurchaseCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET create_purchase is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.SupplierId <= 0)
        {
            return Refuse("dry-run-invalid", "supplier_required", "Supplier is required (PHP).", request);
        }

        if (request.AmountExVat <= 0)
        {
            return Refuse("dry-run-invalid", "amount_required", "amountExVat must be positive.", request);
        }

        return new ErpPurchaseCreateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            request.SupplierId, request.AmountExVat,
            [
                "INSERT INTO `epc_erp_purchases` (…) (NOT executed)",
                "Tax toolkit VAT calc + supplier accounting (NOT executed)",
                "Optional inventory receipt / BOS workflow (NOT executed)"
            ],
            "Payload shape validated; purchase invoice INSERT blocked. VAT/inventory edge cases stay PHP until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=create_purchase");
    }

    private static ErpPurchaseCreateDryRunResult Refuse(
        string status, string code, string detail, ErpPurchaseCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.SupplierId, request.AmountExVat,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=create_purchase");
}

public sealed record ErpPurchaseCreateRequest(long SupplierId, decimal AmountExVat, bool ConfirmWrites = false);

public sealed record ErpPurchaseCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long SupplierId, decimal AmountExVat,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { supplier_id = SupplierId, amount_ex_vat = AmountExVat },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
