namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>automation_deactivate</c> / <c>epc_erp_automation_set_enabled</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpAutomationDeactivateWriteService</c>.
/// </summary>
public interface IErpAutomationDeactivateDryRun
{
    ErpAutomationDeactivateDryRunResult Evaluate(ErpAutomationDeactivateRequest request);
}

public sealed class ErpAutomationDeactivateDryRun : IErpAutomationDeactivateDryRun
{
    public ErpAutomationDeactivateDryRunResult Evaluate(ErpAutomationDeactivateRequest request)
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

        return new ErpAutomationDeactivateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id ?? "",
            ["INSERT `epc_price_settings` ON DUPLICATE KEY UPDATE (NOT executed)"],
            "ErpAutomationDeactivate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_automation_catalogue.php");
    }

    private static ErpAutomationDeactivateDryRunResult Refuse(string s, string c, string d, ErpAutomationDeactivateRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id ?? "", [], d, "content/shop/finance/epc_erp_automation_catalogue.php");
}

public sealed record ErpAutomationDeactivateRequest(bool ConfirmWrites = false, string? Id = null);

public sealed record ErpAutomationDeactivateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "automation_deactivate", id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
