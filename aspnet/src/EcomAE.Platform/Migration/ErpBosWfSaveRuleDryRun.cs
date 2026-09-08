namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_wf_save_rule</c> / <c>epc_bos_wf_save_rule</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpBosWfSaveRuleWriteService</c>.
/// </summary>
public interface IErpBosWfSaveRuleDryRun
{
    ErpBosWfSaveRuleDryRunResult Evaluate(ErpBosWfSaveRuleRequest request);
}

public sealed class ErpBosWfSaveRuleDryRun : IErpBosWfSaveRuleDryRun
{
    public ErpBosWfSaveRuleDryRunResult Evaluate(ErpBosWfSaveRuleRequest request)
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

        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.EntityType))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Name and document type required", request);
        }

        return new ErpBosWfSaveRuleDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.Name, request.EntityType,
            ["INSERT/UPDATE `epc_bos_approval_rules` (NOT executed)"],
            "ErpBosWfSaveRule payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_workflow.php");
    }

    private static ErpBosWfSaveRuleDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpBosWfSaveRuleRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.Name, request.EntityType, [], detail,
            "content/shop/finance/epc_bos_workflow.php");
}

public sealed record ErpBosWfSaveRuleRequest(
    long Id = 0,
    string? Name = null,
    string? EntityType = null,
    bool ConfirmWrites = false);

public sealed record ErpBosWfSaveRuleDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Name, string? EntityType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, name = Name, entityType = EntityType, action = "bos_wf_save_rule" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
