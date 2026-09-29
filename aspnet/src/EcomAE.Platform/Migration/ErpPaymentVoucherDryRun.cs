namespace EcomAE.Platform.Migration;

/// <summary>
/// Validation envelope for the live ASP.NET payment-voucher writer.
/// </summary>
public interface IErpPaymentVoucherDryRun
{
    ErpPaymentVoucherDryRunResult Evaluate(ErpPaymentVoucherRequest request);
}

public sealed class ErpPaymentVoucherDryRun : IErpPaymentVoucherDryRun
{
    public ErpPaymentVoucherDryRunResult Evaluate(ErpPaymentVoucherRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "Use confirm_writes=true to execute the live ASP.NET payment-voucher writer.",
                request);
        }

        if (request.SupplierId <= 0 || request.AccountId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "Supplier, bank account, and amount required (PHP).", request);
        }

        return new ErpPaymentVoucherDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: false,
            ValidationCode: "ok",
            WouldWrite: true,
            SupplierId: request.SupplierId,
            AccountId: request.AccountId,
            Amount: request.Amount,
            SimulatedSql:
            [
                "INSERT INTO `epc_erp_cash_bank_entries` payment (…) (NOT executed)",
                "Settlement allocate / FIFO against open bills (NOT executed)",
                "GL Dr AP / Cr Bank (NOT executed)"
            ],
            Detail: "Payload shape validated; no write was performed.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=payment_voucher");
    }

    private static ErpPaymentVoucherDryRunResult Refuse(
        string status, string code, string detail, ErpPaymentVoucherRequest request) =>
        new(status, 0, true, false, false, code, false, request.SupplierId, request.AccountId, request.Amount,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=payment_voucher");
}

public sealed record ErpPaymentVoucherRequest(
    long SupplierId,
    long AccountId,
    decimal Amount,
    bool ConfirmWrites = false);

public sealed record ErpPaymentVoucherDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long SupplierId, long AccountId, decimal Amount,
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
        intended = new { supplier_id = SupplierId, account_id = AccountId, amount = Amount },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
