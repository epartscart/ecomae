namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_erp_payroll_pay_run</c> when
/// <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpPayrollPayWriteService</c>. Schema ensure and COA GL stay PHP.
/// </summary>
public interface IErpPayrollPayDryRun
{
    ErpPayrollPayDryRunResult Evaluate(ErpPayrollPayRequest request);
}

public sealed class ErpPayrollPayDryRun : IErpPayrollPayDryRun
{
    public ErpPayrollPayDryRunResult Evaluate(ErpPayrollPayRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "id must be >= 0.", request);
        }

        return new ErpPayrollPayDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.CashAccountId,
            ["INSERT cash payment + UPDATE run/lines to paid (NOT executed)"],
            "ErpPayrollPay payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_payroll.php");
    }

    private static ErpPayrollPayDryRunResult Refuse(
        string status, string code, string detail, ErpPayrollPayRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.CashAccountId, [], detail,
            "content/shop/finance/epc_erp_payroll.php");
}

public sealed record ErpPayrollPayRequest(long Id = 0, long CashAccountId = 0, bool ConfirmWrites = false);

public sealed record ErpPayrollPayDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, long CashAccountId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { run_id = Id, cash_account_id = CashAccountId, action = "payroll_pay" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
