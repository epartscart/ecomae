namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>hrt_job_save</c> / <c>epc_hrt_job_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpHrtJobSaveWriteService</c>.
/// </summary>
public interface IErpHrtJobSaveDryRun
{
    ErpHrtJobSaveDryRunResult Evaluate(ErpHrtJobSaveRequest request);
}

public sealed class ErpHrtJobSaveDryRun : IErpHrtJobSaveDryRun
{
    public ErpHrtJobSaveDryRunResult Evaluate(ErpHrtJobSaveRequest request)
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

        return new ErpHrtJobSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Title,
            ["UPDATE / INSERT `epc_hrt_job` (NOT executed)"],
            "ErpHrtJobSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr_talent.php");
    }

    private static ErpHrtJobSaveDryRunResult Refuse(string s, string c, string d, ErpHrtJobSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Title, [], d, "content/shop/finance/epc_erp_hr_talent.php");
}

public sealed record ErpHrtJobSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Title = null,
    string? Department = null,
    int Headcount = 1,
    string? HiringManager = null,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpHrtJobSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Title,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "hrt_job_save", id = Id, title = Title },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
