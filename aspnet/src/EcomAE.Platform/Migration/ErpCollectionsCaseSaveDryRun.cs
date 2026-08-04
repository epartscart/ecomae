namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>coll_case_save</c>. Never INSERT/UPDATE. PHP authoritative.</summary>
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
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET coll_case_save is not implemented; PHP ajax_erp.php remains authoritative.", request);

        if (request.CustomerId < 0)
            return Refuse("dry-run-invalid", "invalid_request", "customerId must be >= 0.", request);

        return new ErpCollectionsCaseSaveDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            request.CustomerId, request.Id,
            ["epc_coll_case_save(@data, @id) (NOT executed)"],
            "Collections case save payload validated; write blocked. company_id stays PHP context.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=coll_case_save");
    }

    private static ErpCollectionsCaseSaveDryRunResult Refuse(string status, string code, string detail, ErpCollectionsCaseSaveRequest request) =>
        new(status, 0, true, false, true, code, false, request.CustomerId, request.Id, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=coll_case_save");
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
