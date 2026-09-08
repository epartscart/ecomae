using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_hr_payroll_run</c> when <c>confirmWrites</c> is omitted.
/// Live draft/payslip replace is <c>IErpHrPayrollRunWriteService</c>. Schema ensure stays PHP.
/// </summary>
public interface IErpHrPayrollRunDryRun
{
    ErpHrPayrollRunDryRunResult Evaluate(ErpHrPayrollRunRequest request);
}

public sealed class ErpHrPayrollRunDryRun : IErpHrPayrollRunDryRun
{
    public ErpHrPayrollRunDryRunResult Evaluate(ErpHrPayrollRunRequest request)
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

        var period = ErpHrPayrollRunWriteService.NormalizePeriod(request.Period);
        if (period is null)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Invalid period (use YYYY-MM)", request);
        }

        return new ErpHrPayrollRunDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            period,
            ["INSERT/UPDATE `epc_hr_payroll_runs` + REPLACE `epc_hr_payslips` (NOT executed)"],
            "ErpHrPayrollRun payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr.php");
    }

    private static ErpHrPayrollRunDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpHrPayrollRunRequest request) =>
        new(status, 0, true, false, false, code, false, request.Period, [], detail,
            "content/shop/finance/epc_erp_hr.php");
}

public sealed record ErpHrPayrollRunRequest(
    string? Period = null,
    bool ConfirmWrites = false);

public sealed record ErpHrPayrollRunDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Period,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { period = Period, action = "hr_payroll_run" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
