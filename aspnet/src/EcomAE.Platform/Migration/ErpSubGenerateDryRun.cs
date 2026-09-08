namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_sub_generate_invoice</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT/UPDATE is <c>IErpSubGenerateWriteService</c>. Schema ensure stays PHP.
/// </summary>
public interface IErpSubGenerateDryRun
{
    ErpSubGenerateDryRunResult Evaluate(ErpSubGenerateRequest request);
}

public sealed class ErpSubGenerateDryRun : IErpSubGenerateDryRun
{
    public ErpSubGenerateDryRunResult Evaluate(ErpSubGenerateRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "Subscription not found", request);
        }

        return new ErpSubGenerateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["INSERT `epc_erp_sub_invoices` + UPDATE `next_bill_date` (NOT executed)"],
            "ErpSubGenerate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_subscriptions.php");
    }

    private static ErpSubGenerateDryRunResult Refuse(string status, string code, string detail, ErpSubGenerateRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, [], detail,
            "content/shop/finance/epc_erp_subscriptions.php");
}

public sealed record ErpSubGenerateRequest(long Id = 0, bool ConfirmWrites = false);

public sealed record ErpSubGenerateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, action = "sub_generate" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
