namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>hrt_applicant_stage</c> / <c>epc_hrt_applicant_set_stage</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpHrtApplicantStageWriteService</c>.
/// </summary>
public interface IErpHrtApplicantStageDryRun
{
    ErpHrtApplicantStageDryRunResult Evaluate(ErpHrtApplicantStageRequest request);
}

public sealed class ErpHrtApplicantStageDryRun : IErpHrtApplicantStageDryRun
{
    public ErpHrtApplicantStageDryRunResult Evaluate(ErpHrtApplicantStageRequest request)
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

        return new ErpHrtApplicantStageDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Stage,
            ["UPDATE `epc_hrt_applicant` stage (NOT executed)"],
            "ErpHrtApplicantStage payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr_talent.php");
    }

    private static ErpHrtApplicantStageDryRunResult Refuse(string s, string c, string d, ErpHrtApplicantStageRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Stage, [], d, "content/shop/finance/epc_erp_hr_talent.php");
}

public sealed record ErpHrtApplicantStageRequest(long Id = 0, string? Stage = null, bool ConfirmWrites = false);

public sealed record ErpHrtApplicantStageDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Stage,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "hrt_applicant_stage", id = Id, stage = Stage },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
