namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_mcgl_set_rate</c> when <c>confirmWrites</c> is omitted.
/// Live writes go through <c>IErpMultiCurrencyGlWriteService</c>.
/// </summary>
public interface IErpMultiCurrencyGlWriteDryRun
{
    ErpMultiCurrencyGlWriteDryRunResult Evaluate(ErpMultiCurrencyGlWriteRequest request);
}

public sealed class ErpMultiCurrencyGlWriteDryRun : IErpMultiCurrencyGlWriteDryRun
{
    public ErpMultiCurrencyGlWriteDryRunResult Evaluate(ErpMultiCurrencyGlWriteRequest request)
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
            ["content/shop/finance/epc_multi_currency_gl.php (NOT executed)"],
            "ErpMultiCurrencyGlWrite payload validated; UPSERT blocked until confirmWrites=true.",
            "content/shop/finance/epc_multi_currency_gl.php");
    }

    private static ErpMultiCurrencyGlWriteDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpMultiCurrencyGlWriteRequest request)
        => new(status, 0, true, false, false, code, false, request.Action, [], detail, "content/shop/finance/epc_multi_currency_gl.php");
}

public sealed record ErpMultiCurrencyGlWriteRequest(string? Action = null, bool ConfirmWrites = false);

public sealed record ErpMultiCurrencyGlWriteDryRunResult(
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
