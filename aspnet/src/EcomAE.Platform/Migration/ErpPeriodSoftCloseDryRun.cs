namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>period_soft_close</c> / <c>epc_erp_period_soft_close</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpPeriodSoftCloseWriteService</c>.
/// </summary>
public interface IErpPeriodSoftCloseDryRun
{
    ErpPeriodSoftCloseDryRunResult Evaluate(ErpPeriodSoftCloseRequest request);
}

public sealed class ErpPeriodSoftCloseDryRun : IErpPeriodSoftCloseDryRun
{
    public ErpPeriodSoftCloseDryRunResult Evaluate(ErpPeriodSoftCloseRequest request)
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

        var ym = (request.YearMonth ?? string.Empty).Trim();
        if (ym.Length != 7 || ym[4] != '-' || !int.TryParse(ym[..4], out _) || !int.TryParse(ym[5..], out var month) || month is < 1 or > 12)
        {
            return Refuse("dry-run-invalid", "year_month_required",
                "yearMonth must be YYYY-MM (PHP).", request);
        }

        return new ErpPeriodSoftCloseDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, ym, request.Note,
            [
                "UPDATE `epc_erp_periods` SET status='soft_close' (NOT executed)",
                "INSERT `epc_erp_period_close_log` (NOT executed)"
            ],
            "ErpPeriodSoftClose payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_period_close.php");
    }

    private static ErpPeriodSoftCloseDryRunResult Refuse(
        string status, string code, string detail, ErpPeriodSoftCloseRequest request) =>
        new(status, 0, true, false, false, code, false, request.YearMonth, request.Note, [], detail,
            "content/shop/finance/epc_erp_period_close.php");
}

public sealed record ErpPeriodSoftCloseRequest(string? YearMonth, string? Note = null, bool ConfirmWrites = false);

public sealed record ErpPeriodSoftCloseDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? YearMonth, string? Note,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { year_month = YearMonth, note = Note },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
