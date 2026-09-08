namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>fin_alloc_save</c> / <c>epc_fin_alloc_rule_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpFinAllocSaveWriteService</c>.
/// </summary>
public interface IErpFinAllocSaveDryRun
{
    ErpFinAllocSaveDryRunResult Evaluate(ErpFinAllocSaveRequest request);
}

public sealed class ErpFinAllocSaveDryRun : IErpFinAllocSaveDryRun
{
    public ErpFinAllocSaveDryRunResult Evaluate(ErpFinAllocSaveRequest request)
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

        return new ErpFinAllocSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPSERT `epc_fin_alloc_rule` (NOT executed)"],
            "ErpFinAllocSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_fin_advanced.php");
    }

    private static ErpFinAllocSaveDryRunResult Refuse(string s, string c, string d, ErpFinAllocSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_fin_advanced.php");
}

public sealed record ErpFinAllocSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? SourceAccount = null,
    string? Basis = null,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpFinAllocSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "fin_alloc_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
