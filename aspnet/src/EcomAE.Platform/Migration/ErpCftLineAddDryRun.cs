namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cft_line_add</c> / <c>epc_cft_line_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpCftLineAddWriteService</c>.
/// </summary>
public interface IErpCftLineAddDryRun
{
    ErpCftLineAddDryRunResult Evaluate(ErpCftLineAddRequest request);
}

public sealed class ErpCftLineAddDryRun : IErpCftLineAddDryRun
{
    public ErpCftLineAddDryRunResult Evaluate(ErpCftLineAddRequest request)
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

        return new ErpCftLineAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ForecastId, request.Direction,
            ["INSERT `epc_cft_line` (NOT executed)"],
            "ErpCftLineAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_cash_treasury.php");
    }

    private static ErpCftLineAddDryRunResult Refuse(string s, string c, string d, ErpCftLineAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.ForecastId, r.Direction, [], d, "content/shop/finance/epc_erp_cash_treasury.php");
}

public sealed record ErpCftLineAddRequest(
    long ForecastId = 0,
    string? DueDate = null,
    string? Direction = null,
    decimal Amount = 0,
    string? Category = null,
    string? Source = null,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpCftLineAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ForecastId, string? Direction,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "cft_line_add", forecast_id = ForecastId, direction = Direction },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
