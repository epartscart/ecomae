namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_wf_raise_test</c> / <c>epc_bos_wf_raise</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpBosWfRaiseWriteService</c>.
/// </summary>
public interface IErpBosWfRaiseTestDryRun
{
    ErpBosWfRaiseTestDryRunResult Evaluate(ErpBosWfRaiseTestRequest request);
}

public sealed class ErpBosWfRaiseTestDryRun : IErpBosWfRaiseTestDryRun
{
    public ErpBosWfRaiseTestDryRunResult Evaluate(ErpBosWfRaiseTestRequest request)
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

        return new ErpBosWfRaiseTestDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.EntityType, request.EntityId, request.Amount,
            ["INSERT `epc_bos_approval_requests` + `epc_bos_approval_log` (NOT executed)"],
            "ErpBosWfRaise payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_workflow.php");
    }

    private static ErpBosWfRaiseTestDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpBosWfRaiseTestRequest request) =>
        new(status, 0, true, false, false, code, false, request.EntityType, request.EntityId, request.Amount, [], detail,
            "content/shop/finance/epc_bos_workflow.php");
}

public sealed record ErpBosWfRaiseTestRequest(
    string? EntityType = null,
    long EntityId = 0,
    string? EntityRef = null,
    decimal Amount = 0,
    string? Title = null,
    bool ConfirmWrites = false);

public sealed record ErpBosWfRaiseTestDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? EntityType, long EntityId, decimal Amount,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { entityType = EntityType, entityId = EntityId, amount = Amount, action = "bos_wf_raise_test" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
