namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>plt_job_run</c> / <c>epc_plt_batch_run</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpPltJobRunWriteService</c>.
/// </summary>
public interface IErpPltJobRunDryRun
{
    ErpPltJobRunDryRunResult Evaluate(ErpPltJobRunRequest request);
}

public sealed class ErpPltJobRunDryRun : IErpPltJobRunDryRun
{
    public ErpPltJobRunDryRunResult Evaluate(ErpPltJobRunRequest request)
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

        var status = request.Status ?? "ended";
        var invalid = EcomAE.Platform.Erp.ErpPltJobRunWriteService.Validate(status);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpPltJobRunDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.JobId, status,
            ["INSERT `epc_plt_batch_run`; UPDATE `epc_plt_batch_job` (NOT executed)"],
            "ErpPltJobRun payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_platform.php");
    }

    private static ErpPltJobRunDryRunResult Refuse(string s, string c, string d, ErpPltJobRunRequest r) =>
        new(s, 0, true, false, false, c, false, r.JobId, r.Status, [], d, "content/shop/finance/epc_erp_platform.php");
}

public sealed record ErpPltJobRunRequest(
    long JobId = 0,
    string? Status = null,
    string? Message = null,
    bool ConfirmWrites = false);

public sealed record ErpPltJobRunDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long JobId, string? JobStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "plt_job_run", job_id = JobId, job_status = JobStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
