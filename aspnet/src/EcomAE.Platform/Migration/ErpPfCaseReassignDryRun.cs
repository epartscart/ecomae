namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pf_case_reassign</c> / <c>epc_pf_case_reassign</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpPfCaseReassignWriteService</c>.
/// </summary>
public interface IErpPfCaseReassignDryRun
{
    ErpPfCaseReassignDryRunResult Evaluate(ErpPfCaseReassignRequest request);
}

public sealed class ErpPfCaseReassignDryRun : IErpPfCaseReassignDryRun
{
    public ErpPfCaseReassignDryRunResult Evaluate(ErpPfCaseReassignRequest request)
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

        if (request.Id <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Case is not open", request);
        }

        if (request.UserId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select an assignee", request);
        }

        return new ErpPfCaseReassignDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.UserId,
            ["UPDATE `epc_pf_cases` + `epc_pf_case_steps` (NOT executed)"],
            "ErpPfCaseReassign payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_processflow.php");
    }

    private static ErpPfCaseReassignDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPfCaseReassignRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.UserId, [], detail,
            "content/shop/finance/epc_erp_processflow.php");
}

public sealed record ErpPfCaseReassignRequest(
    long Id = 0,
    long UserId = 0,
    bool ConfirmWrites = false);

public sealed record ErpPfCaseReassignDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, long UserId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, userId = UserId, action = "pf_case_reassign" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
