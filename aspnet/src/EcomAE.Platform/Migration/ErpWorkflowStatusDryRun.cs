namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>workflow_status</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpWorkflowStatusDryRun
{
    ErpWorkflowStatusDryRunResult Evaluate(ErpWorkflowStatusRequest request);
}

public sealed class ErpWorkflowStatusDryRun : IErpWorkflowStatusDryRun
{
    public static readonly IReadOnlyList<string> Allowed =
        ["pending", "in_progress", "done", "cancelled"];

    public ErpWorkflowStatusDryRunResult Evaluate(ErpWorkflowStatusRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET workflow_status is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        if (request.TaskId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "taskId must be positive.", request);
        }

        var status = (request.Status ?? "done").Trim().ToLowerInvariant();
        if (!Allowed.Contains(status, StringComparer.Ordinal))
        {
            return Refuse("dry-run-invalid", "invalid_status",
                "Invalid status (PHP: pending|in_progress|done|cancelled).", request);
        }

        return new ErpWorkflowStatusDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, request.TaskId, status,
            ["UPDATE `epc_erp_workflow_tasks` SET status/completed_at WHERE id=@task (NOT executed)"],
            "Workflow status payload validated; UPDATE blocked until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=workflow_status");
    }

    private static ErpWorkflowStatusDryRunResult Refuse(
        string status, string code, string detail, ErpWorkflowStatusRequest request) =>
        new(status, 0, true, false, true, code, false, request.TaskId, request.Status, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=workflow_status");
}

public sealed record ErpWorkflowStatusRequest(long TaskId, string? Status = "done", bool ConfirmWrites = false);

public sealed record ErpWorkflowStatusDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long TaskId, string? TaskStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { task_id = TaskId, status = TaskStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
