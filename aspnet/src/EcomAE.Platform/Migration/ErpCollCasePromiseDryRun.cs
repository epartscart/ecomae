namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>coll_case_promise</c> / <c>epc_coll_case_promise</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpCollectionsCasePromiseWriteService</c>.
/// </summary>
public interface IErpCollCasePromiseDryRun
{
    ErpCollCasePromiseDryRunResult Evaluate(ErpCollCasePromiseRequest request);
}

public sealed class ErpCollCasePromiseDryRun : IErpCollCasePromiseDryRun
{
    public ErpCollCasePromiseDryRunResult Evaluate(ErpCollCasePromiseRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "id must be positive.", request);
        }

        return new ErpCollCasePromiseDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["UPDATE `epc_coll_cases` + INSERT `epc_coll_activity` (NOT executed)"],
            "ErpCollCasePromise payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_collections.php");
    }

    private static ErpCollCasePromiseDryRunResult Refuse(string s, string c, string d, ErpCollCasePromiseRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, [], d, "content/shop/finance/epc_erp_collections.php");
}

public sealed record ErpCollCasePromiseRequest(
    long Id,
    bool ConfirmWrites = false,
    decimal Amount = 0,
    string? PromiseDate = null);

public sealed record ErpCollCasePromiseDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
