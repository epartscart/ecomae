namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cft_forecast_save</c> / <c>epc_cft_forecast_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpCftForecastSaveWriteService</c>.
/// </summary>
public interface IErpCftForecastSaveDryRun
{
    ErpCftForecastSaveDryRunResult Evaluate(ErpCftForecastSaveRequest request);
}

public sealed class ErpCftForecastSaveDryRun : IErpCftForecastSaveDryRun
{
    public ErpCftForecastSaveDryRunResult Evaluate(ErpCftForecastSaveRequest request)
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

        return new ErpCftForecastSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Name,
            ["UPDATE / INSERT `epc_cft_forecast` (NOT executed)"],
            "ErpCftForecastSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_cash_treasury.php");
    }

    private static ErpCftForecastSaveDryRunResult Refuse(string s, string c, string d, ErpCftForecastSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Name, [], d, "content/shop/finance/epc_erp_cash_treasury.php");
}

public sealed record ErpCftForecastSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    decimal OpeningBalance = 0,
    string? Currency = null,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpCftForecastSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "cft_forecast_save", id = Id, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
