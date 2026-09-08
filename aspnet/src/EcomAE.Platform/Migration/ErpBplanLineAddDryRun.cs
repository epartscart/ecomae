namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bplan_line_add</c> / <c>epc_bplan_line_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpBplanLineAddWriteService</c>.
/// </summary>
public interface IErpBplanLineAddDryRun
{
    ErpBplanLineAddDryRunResult Evaluate(ErpBplanLineAddRequest request);
}

public sealed class ErpBplanLineAddDryRun : IErpBplanLineAddDryRun
{
    public ErpBplanLineAddDryRunResult Evaluate(ErpBplanLineAddRequest request)
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

        return new ErpBplanLineAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.PlanId, request.Account,
            ["INSERT `epc_bplan_line` (NOT executed)"],
            "ErpBplanLineAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_budget_planning.php");
    }

    private static ErpBplanLineAddDryRunResult Refuse(string s, string c, string d, ErpBplanLineAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.PlanId, r.Account, [], d, "content/shop/finance/epc_erp_budget_planning.php");
}

public sealed record ErpBplanLineAddRequest(
    long PlanId = 0,
    string? Account = null,
    string? Dimension = null,
    string? Scenario = null,
    string? Period = null,
    decimal Amount = 0,
    bool ConfirmWrites = false);

public sealed record ErpBplanLineAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PlanId, string? Account,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "bplan_line_add", plan_id = PlanId, account = Account },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
