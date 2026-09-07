namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_hr_leave_request</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT is <c>IErpHrLeaveRequestWriteService</c>.
/// </summary>
public interface IErpHrLeaveRequestDryRun
{
    ErpHrLeaveRequestDryRunResult Evaluate(ErpHrLeaveRequestRequest request);
}

public sealed class ErpHrLeaveRequestDryRun : IErpHrLeaveRequestDryRun
{
    public ErpHrLeaveRequestDryRunResult Evaluate(ErpHrLeaveRequestRequest request)
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

        return new ErpHrLeaveRequestDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.EmployeeId, request.Type,
            ["INSERT `epc_hr_leave` (NOT executed)"],
            "ErpHrLeaveRequest payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr.php");
    }

    private static ErpHrLeaveRequestDryRunResult Refuse(string status, string code, string detail, ErpHrLeaveRequestRequest request) =>
        new(status, 0, true, false, false, code, false, request.EmployeeId, request.Type, [], detail,
            "content/shop/finance/epc_erp_hr.php");
}

public sealed record ErpHrLeaveRequestRequest(
    long EmployeeId = 0, string? Type = null, bool ConfirmWrites = false);
public sealed record ErpHrLeaveRequestDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long EmployeeId, string? Type,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { employee_id = EmployeeId, type = Type },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
