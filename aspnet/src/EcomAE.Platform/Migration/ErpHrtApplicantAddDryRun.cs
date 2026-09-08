namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>hrt_applicant_add</c> / <c>epc_hrt_applicant_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpHrtApplicantAddWriteService</c>.
/// </summary>
public interface IErpHrtApplicantAddDryRun
{
    ErpHrtApplicantAddDryRunResult Evaluate(ErpHrtApplicantAddRequest request);
}

public sealed class ErpHrtApplicantAddDryRun : IErpHrtApplicantAddDryRun
{
    public ErpHrtApplicantAddDryRunResult Evaluate(ErpHrtApplicantAddRequest request)
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

        return new ErpHrtApplicantAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.JobId, request.Name,
            ["INSERT `epc_hrt_applicant` (NOT executed)"],
            "ErpHrtApplicantAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr_talent.php");
    }

    private static ErpHrtApplicantAddDryRunResult Refuse(string s, string c, string d, ErpHrtApplicantAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.JobId, r.Name, [], d, "content/shop/finance/epc_erp_hr_talent.php");
}

public sealed record ErpHrtApplicantAddRequest(
    long JobId = 0,
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    int Rating = 0,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpHrtApplicantAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long JobId, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "hrt_applicant_add", job_id = JobId, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
