namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>sub_save</c>. Never INSERT/UPDATE. PHP authoritative.</summary>
public interface IErpSubscriptionSaveDryRun
{
    ErpSubscriptionSaveDryRunResult Evaluate(ErpSubscriptionSaveRequest request);
}

public sealed class ErpSubscriptionSaveDryRun : IErpSubscriptionSaveDryRun
{
    public ErpSubscriptionSaveDryRunResult Evaluate(ErpSubscriptionSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET sub_save is not implemented; PHP ajax_erp.php remains authoritative.", request);

        var code = (request.Code ?? string.Empty).Trim();
        var customer = (request.Customer ?? string.Empty).Trim();
        if (code.Length == 0 || customer.Length == 0)
            return Refuse("dry-run-invalid", "code_customer_required",
                "Code and customer are required (PHP).", request);

        return new ErpSubscriptionSaveDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, code, customer, request.Id,
            ["epc_sub_save(@data, @id) INSERT/UPDATE (NOT executed)"],
            "Subscription save payload validated; write blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=sub_save");
    }

    private static ErpSubscriptionSaveDryRunResult Refuse(string status, string code, string detail, ErpSubscriptionSaveRequest request) =>
        new(status, 0, true, false, true, code, false, request.Code, request.Customer, request.Id, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=sub_save");
}

public sealed record ErpSubscriptionSaveRequest(string? Code, string? Customer, long Id = 0, bool ConfirmWrites = false);
public sealed record ErpSubscriptionSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code, string? Customer, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { code = Code, customer = Customer, id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
