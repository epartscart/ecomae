namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>workflow_create</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpWorkflowCreateDryRun
{
    ErpWorkflowCreateDryRunResult Evaluate(ErpWorkflowCreateRequest request);
}

public sealed class ErpWorkflowCreateDryRun : IErpWorkflowCreateDryRun
{
    public ErpWorkflowCreateDryRunResult Evaluate(ErpWorkflowCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET workflow_create is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return Refuse("dry-run-invalid", "title_required", "title is required.", request);
        }

        if (title.Length > 255)
        {
            title = title[..255];
        }

        var dept = string.IsNullOrWhiteSpace(request.DepartmentCode) ? "admin" : request.DepartmentCode.Trim();
        var priority = (request.Priority ?? "normal").Trim().ToLowerInvariant();
        if (priority is not ("low" or "normal" or "high"))
        {
            priority = "normal";
        }

        return new ErpWorkflowCreateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            title, dept, priority, request.OrderId,
            ["INSERT INTO `epc_erp_workflow_tasks` (…) (NOT executed)"],
            "Workflow task create payload validated; INSERT blocked until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=workflow_create");
    }

    private static ErpWorkflowCreateDryRunResult Refuse(
        string status, string code, string detail, ErpWorkflowCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.Title, request.DepartmentCode, request.Priority, request.OrderId,
            [], detail, "/CP/content/shop/finance/erp/ajax_erp.php?action=workflow_create");
}

public sealed record ErpWorkflowCreateRequest(
    string? Title,
    string? DepartmentCode = "admin",
    string? Priority = "normal",
    long OrderId = 0,
    bool ConfirmWrites = false);

public sealed record ErpWorkflowCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Title, string? DepartmentCode, string? Priority, long OrderId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { title = Title, department_code = DepartmentCode, priority = Priority, order_id = OrderId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
