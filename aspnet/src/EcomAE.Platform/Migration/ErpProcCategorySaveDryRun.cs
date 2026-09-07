namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>proc_category_save</c> / <c>epc_proc_category_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpProcCategorySaveWriteService</c>.
/// </summary>
public interface IErpProcCategorySaveDryRun
{
    ErpProcCategorySaveDryRunResult Evaluate(ErpProcCategorySaveRequest request);
}

public sealed class ErpProcCategorySaveDryRun : IErpProcCategorySaveDryRun
{
    public ErpProcCategorySaveDryRunResult Evaluate(ErpProcCategorySaveRequest request)
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

        return new ErpProcCategorySaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPDATE / INSERT `epc_proc_category` (NOT executed)"],
            "ErpProcCategorySave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_procurement.php");
    }

    private static ErpProcCategorySaveDryRunResult Refuse(string s, string c, string d, ErpProcCategorySaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_procurement.php");
}

public sealed record ErpProcCategorySaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    long ParentId = 0,
    string? DefaultAccount = null,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpProcCategorySaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "proc_category_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
