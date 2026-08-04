namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>period_lock</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPeriodLockDryRun
{
    ErpPeriodLockDryRunResult Evaluate(ErpPeriodLockRequest request);
}

public sealed class ErpPeriodLockDryRun : IErpPeriodLockDryRun
{
    public ErpPeriodLockDryRunResult Evaluate(ErpPeriodLockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET period_lock is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var ym = (request.YearMonth ?? string.Empty).Trim();
        if (ym.Length != 7 || ym[4] != '-' || !int.TryParse(ym[..4], out _) || !int.TryParse(ym[5..], out var month) || month is < 1 or > 12)
        {
            return Refuse("dry-run-invalid", "year_month_required",
                "yearMonth must be YYYY-MM (PHP).", request);
        }

        return new ErpPeriodLockDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, ym, request.Note,
            [
                "epc_erp_period_lock(@ym, @admin, @note) (NOT executed)",
                "Soft-close prerequisite + reopen path stay PHP until dual-sample"
            ],
            "Period hard-lock payload validated; status UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=period_lock");
    }

    private static ErpPeriodLockDryRunResult Refuse(
        string status, string code, string detail, ErpPeriodLockRequest request) =>
        new(status, 0, true, false, true, code, false, request.YearMonth, request.Note, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=period_lock");
}

public sealed record ErpPeriodLockRequest(string? YearMonth, string? Note = null, bool ConfirmWrites = false);

public sealed record ErpPeriodLockDryRunResult(
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
