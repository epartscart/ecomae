namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_coll_case_save</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT/UPDATE is <c>IErpCollectionsCaseSaveWriteService</c>.
/// </summary>
public interface IErpCollectionsCaseSaveDryRun
{
    ErpCollectionsCaseSaveDryRunResult Evaluate(ErpCollectionsCaseSaveRequest request);
}

public sealed class ErpCollectionsCaseSaveDryRun : IErpCollectionsCaseSaveDryRun
{
    public ErpCollectionsCaseSaveDryRunResult Evaluate(ErpCollectionsCaseSaveRequest request)
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

        if (request.CustomerId < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "customerId must be >= 0.", request);
        }

        return new ErpCollectionsCaseSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.CustomerId, request.Id,
            ["INSERT/UPDATE `epc_coll_cases` (NOT executed)"],
            "ErpCollectionsCaseSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_collections.php");
    }

    private static ErpCollectionsCaseSaveDryRunResult Refuse(string status, string code, string detail, ErpCollectionsCaseSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.CustomerId, request.Id, [], detail,
            "content/shop/finance/epc_erp_collections.php");
}

public sealed record ErpCollectionsCaseSaveRequest(
    long CustomerId = 0, long Id = 0, bool ConfirmWrites = false);
public sealed record ErpCollectionsCaseSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CustomerId, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { customer_id = CustomerId, id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
