namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>supplier_settlement</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpSupplierSettlementDryRun
{
    ErpSupplierSettlementDryRunResult Evaluate(ErpSupplierSettlementRequest request);
}

public sealed class ErpSupplierSettlementDryRun : IErpSupplierSettlementDryRun
{
    public ErpSupplierSettlementDryRunResult Evaluate(ErpSupplierSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET supplier_settlement is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.SupplierId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Supplier and positive amount required (PHP).", request);
        }

        var direction = (request.Direction ?? "decrease").Trim().ToLowerInvariant();
        if (direction is not ("increase" or "decrease"))
        {
            return Refuse("dry-run-invalid", "invalid_direction",
                "Direction must be increase or decrease payable (PHP).", request);
        }

        return new ErpSupplierSettlementDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            request.SupplierId, request.Amount, direction,
            [
                "INSERT INTO `epc_erp_supplier_accounting` (…) (NOT executed)",
                "Optional GL / purchase link stays PHP until dual-sample"
            ],
            "Supplier settlement payload validated; accounting INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=supplier_settlement");
    }

    private static ErpSupplierSettlementDryRunResult Refuse(
        string status, string code, string detail, ErpSupplierSettlementRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.SupplierId, request.Amount, request.Direction, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=supplier_settlement");
}

public sealed record ErpSupplierSettlementRequest(
    long SupplierId,
    decimal Amount,
    string? Direction = "decrease",
    bool ConfirmWrites = false);

public sealed record ErpSupplierSettlementDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long SupplierId, decimal Amount, string? Direction,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { supplier_id = SupplierId, amount = Amount, direction = Direction },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
