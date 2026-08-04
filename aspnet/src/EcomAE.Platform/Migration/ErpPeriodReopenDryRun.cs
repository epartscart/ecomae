namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>period_reopen</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpPeriodReopenDryRun
{
    ErpPeriodReopenDryRunResult Evaluate(ErpPeriodReopenRequest request);
}

public sealed class ErpPeriodReopenDryRun : IErpPeriodReopenDryRun
{
    public ErpPeriodReopenDryRunResult Evaluate(ErpPeriodReopenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET period_reopen is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var ym = (request.YearMonth ?? string.Empty).Trim();
        if (ym.Length != 7 || ym[4] != '-' || !int.TryParse(ym[..4], out _) || !int.TryParse(ym[5..], out var month) || month is < 1 or > 12)
        {
            return Refuse("dry-run-invalid", "year_month_required", "yearMonth must be YYYY-MM (PHP).", request);
        }

        return new ErpPeriodReopenDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, ym, request.Note,
            ["epc_erp_period_reopen(@ym, @admin, @note) (NOT executed)"],
            "Period reopen payload validated; status UPDATE blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=period_reopen");
    }

    private static ErpPeriodReopenDryRunResult Refuse(
        string status, string code, string detail, ErpPeriodReopenRequest request) =>
        new(status, 0, true, false, true, code, false, request.YearMonth, request.Note, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=period_reopen");
}

public sealed record ErpPeriodReopenRequest(string? YearMonth, string? Note = null, bool ConfirmWrites = false);

public sealed record ErpPeriodReopenDryRunResult(
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
