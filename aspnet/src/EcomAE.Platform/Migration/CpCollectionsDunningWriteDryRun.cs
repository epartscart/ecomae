namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_collections_dunning.php</c> queue mutations when
/// <c>confirmWrites</c> is omitted. Live writes go through
/// <c>ICpCollectionsDunningWriteService</c>.
/// </summary>
public interface ICpCollectionsDunningWriteDryRun
{
    CpCollectionsDunningWriteDryRunResult Evaluate(CpCollectionsDunningWriteRequest request);
}

public sealed class CpCollectionsDunningWriteDryRun : ICpCollectionsDunningWriteDryRun
{
    public CpCollectionsDunningWriteDryRunResult Evaluate(CpCollectionsDunningWriteRequest request)
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

        return new(
            "dry-run-validated",
            0,
            true,
            false,
            false,
            "ok",
            true,
            request.Action,
            ["content/shop/finance/epc_collections_dunning.php (NOT executed)"],
            "CpCollectionsDunningWrite payload validated; UPDATE blocked until confirmWrites=true.",
            "content/shop/finance/epc_collections_dunning.php");
    }

    private static CpCollectionsDunningWriteDryRunResult Refuse(
        string status,
        string code,
        string detail,
        CpCollectionsDunningWriteRequest request)
        => new(status, 0, true, false, false, code, false, request.Action, [], detail, "content/shop/finance/epc_collections_dunning.php");
}

public sealed record CpCollectionsDunningWriteRequest(string? Action = null, bool ConfirmWrites = false);

public sealed record CpCollectionsDunningWriteDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    string? Action,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "cp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { action = Action },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
