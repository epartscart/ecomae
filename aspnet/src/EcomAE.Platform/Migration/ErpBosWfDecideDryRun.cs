namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_wf_decide</c> / <c>epc_bos_wf_decide</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpBosWfDecideWriteService</c>.
/// </summary>
public interface IErpBosWfDecideDryRun
{
    ErpBosWfDecideDryRunResult Evaluate(ErpBosWfDecideRequest request);
}

public sealed class ErpBosWfDecideDryRun : IErpBosWfDecideDryRun
{
    public ErpBosWfDecideDryRunResult Evaluate(ErpBosWfDecideRequest request)
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

        if (request.RequestId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Request not pending", request);
        }

        return new ErpBosWfDecideDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.RequestId, request.Decision,
            ["UPDATE `epc_bos_approval_requests` + INSERT `epc_bos_approval_log` (NOT executed)"],
            "ErpBosWfDecide payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_workflow.php");
    }

    private static ErpBosWfDecideDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpBosWfDecideRequest request) =>
        new(status, 0, true, false, false, code, false, request.RequestId, request.Decision, [], detail,
            "content/shop/finance/epc_bos_workflow.php");
}

public sealed record ErpBosWfDecideRequest(
    long RequestId = 0,
    string? Decision = null,
    string? Comment = null,
    bool ConfirmWrites = false);

public sealed record ErpBosWfDecideDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long RequestId, string? Decision,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { requestId = RequestId, decision = Decision, action = "bos_wf_decide" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
