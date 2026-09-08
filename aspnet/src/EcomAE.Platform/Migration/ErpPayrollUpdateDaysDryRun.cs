namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_erp_payroll_update_line_days</c> when
/// <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpPayrollUpdateDaysWriteService</c>. Schema ensure stays PHP.
/// </summary>
public interface IErpPayrollUpdateDaysDryRun
{
    ErpPayrollUpdateDaysDryRunResult Evaluate(ErpPayrollUpdateDaysRequest request);
}

public sealed class ErpPayrollUpdateDaysDryRun : IErpPayrollUpdateDaysDryRun
{
    public ErpPayrollUpdateDaysDryRunResult Evaluate(ErpPayrollUpdateDaysRequest request)
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

        return new ErpPayrollUpdateDaysDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.DaysWorked,
            ["UPDATE `epc_erp_payroll_lines` + recalc run totals (NOT executed)"],
            "ErpPayrollUpdateDays payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_payroll.php");
    }

    private static ErpPayrollUpdateDaysDryRunResult Refuse(
        string status, string code, string detail, ErpPayrollUpdateDaysRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.DaysWorked, [], detail,
            "content/shop/finance/epc_erp_payroll.php");
}

public sealed record ErpPayrollUpdateDaysRequest(long Id = 0, decimal DaysWorked = 30, bool ConfirmWrites = false);

public sealed record ErpPayrollUpdateDaysDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, decimal DaysWorked,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { line_id = Id, days_worked = DaysWorked, action = "payroll_update_days" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
