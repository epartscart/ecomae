namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_hr_employee_save</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT/UPDATE is <c>IErpHrEmpSaveWriteService</c>. Schema ensure stays PHP.
/// </summary>
public interface IErpHrEmpSaveDryRun
{
    ErpHrEmpSaveDryRunResult Evaluate(ErpHrEmpSaveRequest request);
}

public sealed class ErpHrEmpSaveDryRun : IErpHrEmpSaveDryRun
{
    public ErpHrEmpSaveDryRunResult Evaluate(ErpHrEmpSaveRequest request)
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

        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Code and name are required", request);
        }

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "id must be >= 0.", request);
        }

        return new ErpHrEmpSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.Code,
            ["INSERT/UPDATE `epc_hr_employees` (NOT executed)"],
            "ErpHrEmpSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr.php");
    }

    private static ErpHrEmpSaveDryRunResult Refuse(string status, string code, string detail, ErpHrEmpSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.Code, [], detail,
            "content/shop/finance/epc_erp_hr.php");
}

public sealed record ErpHrEmpSaveRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    string? Name = null);

public sealed record ErpHrEmpSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
