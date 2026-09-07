namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>proc_policy_save</c> / <c>epc_proc_policy_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpProcPolicySaveWriteService</c>.
/// </summary>
public interface IErpProcPolicySaveDryRun
{
    ErpProcPolicySaveDryRunResult Evaluate(ErpProcPolicySaveRequest request);
}

public sealed class ErpProcPolicySaveDryRun : IErpProcPolicySaveDryRun
{
    public ErpProcPolicySaveDryRunResult Evaluate(ErpProcPolicySaveRequest request)
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

        return new ErpProcPolicySaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Name,
            ["UPDATE / INSERT `epc_proc_policy` (NOT executed)"],
            "ErpProcPolicySave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_procurement.php");
    }

    private static ErpProcPolicySaveDryRunResult Refuse(string s, string c, string d, ErpProcPolicySaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Name, [], d, "content/shop/finance/epc_erp_procurement.php");
}

public sealed record ErpProcPolicySaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    long CategoryId = 0,
    decimal ApprovalThreshold = 0,
    string? PreferredVendor = null,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpProcPolicySaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "proc_policy_save", id = Id, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
