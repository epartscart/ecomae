namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bplan_advance</c> / <c>epc_bplan_advance_stage</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpBplanAdvanceWriteService</c>.
/// </summary>
public interface IErpBplanAdvanceDryRun
{
    ErpBplanAdvanceDryRunResult Evaluate(ErpBplanAdvanceRequest request);
}

public sealed class ErpBplanAdvanceDryRun : IErpBplanAdvanceDryRun
{
    public ErpBplanAdvanceDryRunResult Evaluate(ErpBplanAdvanceRequest request)
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

        return new ErpBplanAdvanceDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["UPDATE `epc_bplan_plan` SET stage (NOT executed)"],
            "ErpBplanAdvance payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_budget_planning.php");
    }

    private static ErpBplanAdvanceDryRunResult Refuse(string s, string c, string d, ErpBplanAdvanceRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, [], d, "content/shop/finance/epc_erp_budget_planning.php");
}

public sealed record ErpBplanAdvanceRequest(long Id, bool ConfirmWrites = false);

public sealed record ErpBplanAdvanceDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "bplan_advance", id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
