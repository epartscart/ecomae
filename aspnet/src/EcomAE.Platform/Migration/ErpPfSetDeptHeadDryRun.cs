namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pf_set_dept_head</c> / <c>epc_pf_set_dept_head</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpPfSetDeptHeadWriteService</c>.
/// </summary>
public interface IErpPfSetDeptHeadDryRun
{
    ErpPfSetDeptHeadDryRunResult Evaluate(ErpPfSetDeptHeadRequest request);
}

public sealed class ErpPfSetDeptHeadDryRun : IErpPfSetDeptHeadDryRun
{
    public ErpPfSetDeptHeadDryRunResult Evaluate(ErpPfSetDeptHeadRequest request)
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

        if (string.IsNullOrWhiteSpace(request.DepartmentCode))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Department code is required", request);
        }

        if (request.HeadUserId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select a department head", request);
        }

        return new ErpPfSetDeptHeadDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.DepartmentCode, request.HeadUserId,
            ["INSERT `epc_pf_dept_heads` (NOT executed)"],
            "ErpPfSetDeptHead payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_processflow.php");
    }

    private static ErpPfSetDeptHeadDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPfSetDeptHeadRequest request) =>
        new(status, 0, true, false, false, code, false, request.DepartmentCode, request.HeadUserId, [], detail,
            "content/shop/finance/epc_erp_processflow.php");
}

public sealed record ErpPfSetDeptHeadRequest(
    string? DepartmentCode = null,
    long HeadUserId = 0,
    bool ConfirmWrites = false);

public sealed record ErpPfSetDeptHeadDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? DepartmentCode, long HeadUserId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { departmentCode = DepartmentCode, headUserId = HeadUserId, action = "pf_set_dept_head" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
