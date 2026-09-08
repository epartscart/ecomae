using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_erp_payroll_generate_run</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT is <c>IErpPayrollGenerateWriteService</c>. Schema ensure stays PHP.
/// </summary>
public interface IErpPayrollGenerateDryRun
{
    ErpPayrollGenerateDryRunResult Evaluate(ErpPayrollGenerateRequest request);
}

public sealed class ErpPayrollGenerateDryRun : IErpPayrollGenerateDryRun
{
    public ErpPayrollGenerateDryRunResult Evaluate(ErpPayrollGenerateRequest request)
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

        if (ErpPayrollGenerateWriteService.NormalizePeriodLabel(request.PeriodLabel) is null)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Invalid period (use YYYY-MM)", request);
        }

        return new ErpPayrollGenerateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.PeriodLabel,
            ["INSERT/rebuild `epc_erp_payroll_runs` + lines (NOT executed)"],
            "ErpPayrollGenerate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_payroll.php");
    }

    private static ErpPayrollGenerateDryRunResult Refuse(
        string status, string code, string detail, ErpPayrollGenerateRequest request) =>
        new(status, 0, true, false, false, code, false, request.PeriodLabel, [], detail,
            "content/shop/finance/epc_erp_payroll.php");
}

public sealed record ErpPayrollGenerateRequest(string? PeriodLabel = null, bool ConfirmWrites = false);

public sealed record ErpPayrollGenerateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? PeriodLabel,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { period_label = PeriodLabel, action = "payroll_generate" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
