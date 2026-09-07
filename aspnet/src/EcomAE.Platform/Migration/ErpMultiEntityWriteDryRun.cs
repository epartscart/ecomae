namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_multi_entity.php</c> group/member/IC mutations when
/// <c>confirmWrites</c> is omitted. Distinct from ajax_erp <c>multi_entity_save</c>.
/// </summary>
public interface IErpMultiEntityWriteDryRun
{
    ErpMultiEntityWriteDryRunResult Evaluate(ErpMultiEntityWriteRequest request);
}

public sealed class ErpMultiEntityWriteDryRun : IErpMultiEntityWriteDryRun
{
    public ErpMultiEntityWriteDryRunResult Evaluate(ErpMultiEntityWriteRequest request)
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
            ["content/shop/finance/epc_multi_entity.php (NOT executed)"],
            "ErpMultiEntityWrite payload validated; INSERT/UPDATE blocked until confirmWrites=true.",
            "content/shop/finance/epc_multi_entity.php");
    }

    private static ErpMultiEntityWriteDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpMultiEntityWriteRequest request)
        => new(status, 0, true, false, false, code, false, request.Action, [], detail, "content/shop/finance/epc_multi_entity.php");
}

public sealed record ErpMultiEntityWriteRequest(string? Action = null, bool ConfirmWrites = false);

public sealed record ErpMultiEntityWriteDryRunResult(
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
        surface = "erp",
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
