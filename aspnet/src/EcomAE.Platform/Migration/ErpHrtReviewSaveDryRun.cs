namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>hrt_review_save</c> / <c>epc_hrt_review_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpHrtReviewSaveWriteService</c>.
/// </summary>
public interface IErpHrtReviewSaveDryRun
{
    ErpHrtReviewSaveDryRunResult Evaluate(ErpHrtReviewSaveRequest request);
}

public sealed class ErpHrtReviewSaveDryRun : IErpHrtReviewSaveDryRun
{
    public ErpHrtReviewSaveDryRunResult Evaluate(ErpHrtReviewSaveRequest request)
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

        return new ErpHrtReviewSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.EmployeeName,
            ["UPDATE / INSERT `epc_hrt_review` (NOT executed)"],
            "ErpHrtReviewSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr_talent.php");
    }

    private static ErpHrtReviewSaveDryRunResult Refuse(string s, string c, string d, ErpHrtReviewSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.EmployeeName, [], d, "content/shop/finance/epc_erp_hr_talent.php");
}

public sealed record ErpHrtReviewSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    long EmployeeId = 0,
    string? EmployeeName = null,
    string? Period = null,
    string? Reviewer = null,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpHrtReviewSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? EmployeeName,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "hrt_review_save", id = Id, employee_name = EmployeeName },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
