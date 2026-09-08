namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pf_process_save</c> / <c>epc_pf_process_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpPfProcessSaveWriteService</c>.
/// </summary>
public interface IErpPfProcessSaveDryRun
{
    ErpPfProcessSaveDryRunResult Evaluate(ErpPfProcessSaveRequest request);
}

public sealed class ErpPfProcessSaveDryRun : IErpPfProcessSaveDryRun
{
    public ErpPfProcessSaveDryRunResult Evaluate(ErpPfProcessSaveRequest request)
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

        var name = (request.Name ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpPfProcessSaveWriteService.Validate(name);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpPfProcessSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, name,
            ["INSERT/UPDATE `epc_pf_processes` (NOT executed)"],
            "ErpPfProcessSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_processflow.php");
    }

    private static ErpPfProcessSaveDryRunResult Refuse(string s, string c, string d, ErpPfProcessSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Name, [], d, "content/shop/finance/epc_erp_processflow.php");
}

public sealed record ErpPfProcessSaveRequest(
    long Id = 0,
    string? Name = null,
    string? Description = null,
    string? Category = null,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpPfProcessSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "pf_process_save", id = Id, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
