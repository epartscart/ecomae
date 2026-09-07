namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>hr_expense_save</c> / <c>epc_hr_expense_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpHrExpenseSaveWriteService</c>.
/// </summary>
public interface IErpHrExpenseSaveDryRun
{
    ErpHrExpenseSaveDryRunResult Evaluate(ErpHrExpenseSaveRequest request);
}

public sealed class ErpHrExpenseSaveDryRun : IErpHrExpenseSaveDryRun
{
    public ErpHrExpenseSaveDryRunResult Evaluate(ErpHrExpenseSaveRequest request)
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

        if (request.EmployeeId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select an employee", request);
        }

        if (request.LineCount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Add at least one expense line", request);
        }

        return new ErpHrExpenseSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.EmployeeId, request.Title, request.LineCount,
            ["INSERT `epc_hr_expenses` (NOT executed)"],
            "ErpHrExpenseSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr.php");
    }

    private static ErpHrExpenseSaveDryRunResult Refuse(string status, string code, string detail, ErpHrExpenseSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.EmployeeId, request.Title, request.LineCount, [], detail,
            "content/shop/finance/epc_erp_hr.php");
}

public sealed record ErpHrExpenseSaveRequest(
    long EmployeeId = 0,
    string? Title = null,
    int LineCount = 0,
    bool ConfirmWrites = false);

public sealed record ErpHrExpenseSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long EmployeeId, string? Title, int LineCount,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { employee_id = EmployeeId, title = Title, line_count = LineCount },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
