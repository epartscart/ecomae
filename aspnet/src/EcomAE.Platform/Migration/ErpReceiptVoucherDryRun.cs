namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP ERP <c>receipt_voucher</c> / <c>epc_erp_receipt_voucher</c>.
/// Never executes INSERT. PHP ajax_erp.php remains authoritative.
/// </summary>
public interface IErpReceiptVoucherDryRun
{
    ErpReceiptVoucherDryRunResult Evaluate(ErpReceiptVoucherRequest request);
}

public sealed class ErpReceiptVoucherDryRun : IErpReceiptVoucherDryRun
{
    public ErpReceiptVoucherDryRunResult Evaluate(ErpReceiptVoucherRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET receipt_voucher is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.UserId <= 0 || request.AccountId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Customer, bank account, and amount required (PHP).", request);
        }

        return new ErpReceiptVoucherDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            UserId: request.UserId,
            AccountId: request.AccountId,
            Amount: request.Amount,
            SimulatedSql:
            [
                "INSERT INTO `epc_erp_cash_bank_entries` receipt/advance (…) (NOT executed)",
                "Settlement allocate / FIFO (NOT executed)",
                "GL post cash entry (NOT executed)"
            ],
            Detail: "Payload shape validated; receipt voucher INSERT + settlement blocked. Allocation edge cases stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=receipt_voucher");
    }

    private static ErpReceiptVoucherDryRunResult Refuse(
        string status, string code, string detail, ErpReceiptVoucherRequest request) =>
        new(status, 0, true, false, true, code, false, request.UserId, request.AccountId, request.Amount,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=receipt_voucher");
}

public sealed record ErpReceiptVoucherRequest(
    long UserId,
    long AccountId,
    decimal Amount,
    long? SalesOrderId = null,
    bool ConfirmWrites = false);

public sealed record ErpReceiptVoucherDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long UserId, long AccountId, decimal Amount,
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
        intended = new { user_id = UserId, account_id = AccountId, amount = Amount },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
