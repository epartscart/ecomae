namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>fin_periods_generate</c> / <c>epc_fin_periods_generate</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpFinPeriodsGenerateWriteService</c>.
/// </summary>
public interface IErpFinPeriodsGenerateDryRun
{
    ErpFinPeriodsGenerateDryRunResult Evaluate(ErpFinPeriodsGenerateRequest request);
}

public sealed class ErpFinPeriodsGenerateDryRun : IErpFinPeriodsGenerateDryRun
{
    public ErpFinPeriodsGenerateDryRunResult Evaluate(ErpFinPeriodsGenerateRequest request)
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

        return new ErpFinPeriodsGenerateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            ["INSERT `epc_fin_periods` (NOT executed)"],
            "ErpFinPeriodsGenerate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_fin_advanced.php");
    }

    private static ErpFinPeriodsGenerateDryRunResult Refuse(string s, string c, string d, ErpFinPeriodsGenerateRequest r) =>
        new(s, 0, true, false, false, c, false, [], d, "content/shop/finance/epc_erp_fin_advanced.php");
}

public sealed record ErpFinPeriodsGenerateRequest(bool ConfirmWrites = false, int Fy = 0, int StartMonth = 1);

public sealed record ErpFinPeriodsGenerateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "fin_periods_generate" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
