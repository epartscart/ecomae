namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_cs_save_declaration</c> / <c>epc_cs_submit_declaration</c>
/// when <c>confirmWrites</c> is omitted. Live writes go through
/// <c>ICpCustomShippingWriteService</c>.
/// </summary>
public interface ICpCustomShippingWriteDryRun
{
    CpCustomShippingWriteDryRunResult Evaluate(CpCustomShippingWriteRequest request);
}

public sealed class CpCustomShippingWriteDryRun : ICpCustomShippingWriteDryRun
{
    public CpCustomShippingWriteDryRunResult Evaluate(CpCustomShippingWriteRequest request)
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
            ["content/shop/finance/epc_custom_shipping.php (NOT executed)"],
            "CpCustomShippingWrite payload validated; INSERT/UPDATE blocked until confirmWrites=true.",
            "content/shop/finance/epc_custom_shipping.php");
    }

    private static CpCustomShippingWriteDryRunResult Refuse(
        string status,
        string code,
        string detail,
        CpCustomShippingWriteRequest request)
        => new(status, 0, true, false, false, code, false, request.Action, [], detail, "content/shop/finance/epc_custom_shipping.php");
}

public sealed record CpCustomShippingWriteRequest(string? Action = null, bool ConfirmWrites = false);

public sealed record CpCustomShippingWriteDryRunResult(
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
