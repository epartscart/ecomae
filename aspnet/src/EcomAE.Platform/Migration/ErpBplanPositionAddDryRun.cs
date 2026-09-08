namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bplan_position_add</c> / <c>epc_bplan_position_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpBplanPositionAddWriteService</c>.
/// </summary>
public interface IErpBplanPositionAddDryRun
{
    ErpBplanPositionAddDryRunResult Evaluate(ErpBplanPositionAddRequest request);
}

public sealed class ErpBplanPositionAddDryRun : IErpBplanPositionAddDryRun
{
    public ErpBplanPositionAddDryRunResult Evaluate(ErpBplanPositionAddRequest request)
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

        return new ErpBplanPositionAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.PlanId, request.Title,
            ["INSERT `epc_bplan_position` (NOT executed)"],
            "ErpBplanPositionAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_budget_planning.php");
    }

    private static ErpBplanPositionAddDryRunResult Refuse(string s, string c, string d, ErpBplanPositionAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.PlanId, r.Title, [], d, "content/shop/finance/epc_erp_budget_planning.php");
}

public sealed record ErpBplanPositionAddRequest(
    long PlanId = 0,
    string? Title = null,
    string? Department = null,
    int? Headcount = null,
    decimal AnnualCost = 0,
    string? StartPeriod = null,
    bool ConfirmWrites = false);

public sealed record ErpBplanPositionAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PlanId, string? Title,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "bplan_position_add", plan_id = PlanId, title = Title },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
