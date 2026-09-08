namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pf_step_save</c> / <c>epc_pf_step_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpPfStepSaveWriteService</c>.
/// </summary>
public interface IErpPfStepSaveDryRun
{
    ErpPfStepSaveDryRunResult Evaluate(ErpPfStepSaveRequest request);
}

public sealed class ErpPfStepSaveDryRun : IErpPfStepSaveDryRun
{
    public ErpPfStepSaveDryRunResult Evaluate(ErpPfStepSaveRequest request)
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
        var invalid = EcomAE.Platform.Erp.ErpPfStepSaveWriteService.Validate(request.ProcessId, name);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpPfStepSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ProcessId, name,
            ["INSERT `epc_pf_steps` (NOT executed)"],
            "ErpPfStepSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_processflow.php");
    }

    private static ErpPfStepSaveDryRunResult Refuse(string s, string c, string d, ErpPfStepSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.ProcessId, r.Name, [], d, "content/shop/finance/epc_erp_processflow.php");
}

public sealed record ErpPfStepSaveRequest(
    long ProcessId = 0,
    string? Name = null,
    string? AssignType = null,
    int StepNo = 0,
    int SlaHours = 24,
    bool ConfirmWrites = false);

public sealed record ErpPfStepSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ProcessId, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "pf_step_save", process_id = ProcessId, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
