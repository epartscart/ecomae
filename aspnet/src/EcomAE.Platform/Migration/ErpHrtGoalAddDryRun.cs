namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>hrt_goal_add</c> / <c>epc_hrt_goal_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpHrtGoalAddWriteService</c>.
/// </summary>
public interface IErpHrtGoalAddDryRun
{
    ErpHrtGoalAddDryRunResult Evaluate(ErpHrtGoalAddRequest request);
}

public sealed class ErpHrtGoalAddDryRun : IErpHrtGoalAddDryRun
{
    public ErpHrtGoalAddDryRunResult Evaluate(ErpHrtGoalAddRequest request)
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

        return new ErpHrtGoalAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ReviewId, request.Title,
            ["INSERT `epc_hrt_goal` (NOT executed)"],
            "ErpHrtGoalAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_hr_talent.php");
    }

    private static ErpHrtGoalAddDryRunResult Refuse(string s, string c, string d, ErpHrtGoalAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.ReviewId, r.Title, [], d, "content/shop/finance/epc_erp_hr_talent.php");
}

public sealed record ErpHrtGoalAddRequest(
    long ReviewId = 0,
    string? Title = null,
    decimal? Weight = null,
    string? Target = null,
    int Rating = 0,
    bool ConfirmWrites = false);

public sealed record ErpHrtGoalAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ReviewId, string? Title,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "hrt_goal_add", review_id = ReviewId, title = Title },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
