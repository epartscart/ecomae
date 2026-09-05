namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_vat_refund_save</c> / <c>epc_bos_vat_refund_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpBosVatRefundSaveWriteService</c>.
/// </summary>
public interface IErpBosVatRefundSaveDryRun
{
    ErpBosVatRefundSaveDryRunResult Evaluate(ErpBosVatRefundSaveRequest request);
}

public sealed class ErpBosVatRefundSaveDryRun : IErpBosVatRefundSaveDryRun
{
    public ErpBosVatRefundSaveDryRunResult Evaluate(ErpBosVatRefundSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                request);
        }

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "A refund id must be >= 0.", request);
        }

        return new ErpBosVatRefundSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.InvoiceRef, request.SaleAmount,
            [request.Id > 0
                ? "UPDATE `epc_bos_vat_refunds` (NOT executed)"
                : "INSERT `epc_bos_vat_refunds` (NOT executed)"],
            "ErpBosVatRefundSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_vat_refund.php");
    }

    private static ErpBosVatRefundSaveDryRunResult Refuse(string status, string code, string detail, ErpBosVatRefundSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.InvoiceRef, request.SaleAmount, [], detail,
            "content/shop/finance/epc_bos_vat_refund.php");
}

public sealed record ErpBosVatRefundSaveRequest(
    long Id = 0,
    string? InvoiceRef = null,
    decimal SaleAmount = 0,
    bool ConfirmWrites = false);

public sealed record ErpBosVatRefundSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? InvoiceRef, decimal SaleAmount,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, invoice_ref = InvoiceRef, sale_amount = SaleAmount },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
