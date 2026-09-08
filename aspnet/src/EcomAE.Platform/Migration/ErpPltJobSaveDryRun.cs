namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>plt_job_save</c> / <c>epc_plt_batch_job_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpPltJobSaveWriteService</c>.
/// </summary>
public interface IErpPltJobSaveDryRun
{
    ErpPltJobSaveDryRunResult Evaluate(ErpPltJobSaveRequest request);
}

public sealed class ErpPltJobSaveDryRun : IErpPltJobSaveDryRun
{
    public ErpPltJobSaveDryRunResult Evaluate(ErpPltJobSaveRequest request)
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
        var invalid = EcomAE.Platform.Erp.ErpPltJobSaveWriteService.Validate(code);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpPltJobSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CompanyId, code,
            ["INSERT/UPDATE `epc_plt_batch_job` (NOT executed)"],
            "ErpPltJobSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_platform.php");
    }

    private static ErpPltJobSaveDryRunResult Refuse(string s, string c, string d, ErpPltJobSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.CompanyId, r.Code, [], d, "content/shop/finance/epc_erp_platform.php");
}

public sealed record ErpPltJobSaveRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    int RecurrenceMin = 0,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpPltJobSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CompanyId, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "plt_job_save", company_id = CompanyId, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
