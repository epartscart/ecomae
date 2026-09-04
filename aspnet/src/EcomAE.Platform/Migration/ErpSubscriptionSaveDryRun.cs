namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_sub_save</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT/UPDATE is <c>IErpSubscriptionSaveWriteService</c>.
/// </summary>
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
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                request);
        }

        var code = (request.Code ?? string.Empty).Trim();
        var customer = (request.Customer ?? string.Empty).Trim();
        if (code.Length == 0 || customer.Length == 0)
        {
            return Refuse("dry-run-invalid", "code_customer_required",
                "Code and customer are required (PHP).", request);
        }

        return new ErpSubscriptionSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, code, customer, request.Id,
            ["INSERT/UPDATE `epc_erp_subscriptions` (NOT executed)"],
            "ErpSubscriptionSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_subscriptions.php");
    }

    private static ErpSubscriptionSaveDryRunResult Refuse(string status, string code, string detail, ErpSubscriptionSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Code, request.Customer, request.Id, [], detail,
            "content/shop/finance/epc_erp_subscriptions.php");
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
