namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_hr_attendance_log</c> when <c>confirmWrites</c> is omitted.
/// Live upsert is <c>IErpHrAttendanceWriteService</c>. Schema ensure stays PHP.
/// </summary>
public interface IErpHrAttendanceDryRun
{
    ErpHrAttendanceDryRunResult Evaluate(ErpHrAttendanceRequest request);
}

public sealed class ErpHrAttendanceDryRun : IErpHrAttendanceDryRun
{
    public ErpHrAttendanceDryRunResult Evaluate(ErpHrAttendanceRequest request)
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

        return new ErpHrAttendanceDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.EmployeeId, request.Status,
            ["INSERT/UPDATE `epc_hr_attendance` (NOT executed)"],
            "ErpHrAttendance payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr.php");
    }

    private static ErpHrAttendanceDryRunResult Refuse(string status, string code, string detail, ErpHrAttendanceRequest request) =>
        new(status, 0, true, false, false, code, false, request.EmployeeId, request.Status, [], detail,
            "content/shop/finance/epc_erp_hr.php");
}

public sealed record ErpHrAttendanceRequest(
    long EmployeeId = 0,
    string? Status = null,
    bool ConfirmWrites = false);

public sealed record ErpHrAttendanceDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long EmployeeId, string? AttendanceStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { employee_id = EmployeeId, status = AttendanceStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
