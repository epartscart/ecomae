namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>fiscal_set_lock</c>. Never UPDATE. PHP authoritative.</summary>
public interface IErpFiscalSetLockDryRun
{
    ErpFiscalSetLockDryRunResult Evaluate(ErpFiscalSetLockRequest request);
}

public sealed class ErpFiscalSetLockDryRun : IErpFiscalSetLockDryRun
{
    public ErpFiscalSetLockDryRunResult Evaluate(ErpFiscalSetLockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET fiscal_set_lock is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        // lockDateUnix=0 clears the lock (PHP allows clear).
        if (request.LockDateUnix < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request",
                "lockDateUnix must be >= 0 (0 clears lock).", request);
        }

        var clearing = request.LockDateUnix == 0;
        return new ErpFiscalSetLockDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            request.LockDateUnix, request.Note, clearing,
            [
                clearing
                    ? "epc_erp_fiscal_set_lock(0, @note) clear (NOT executed)"
                    : "epc_erp_fiscal_set_lock(@lockDate, @note) (NOT executed)"
            ],
            clearing
                ? "Fiscal lock clear simulated; write blocked."
                : "Fiscal lock set simulated; write blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fiscal_set_lock");
    }

    private static ErpFiscalSetLockDryRunResult Refuse(
        string status, string code, string detail, ErpFiscalSetLockRequest request) =>
        new(status, 0, true, false, true, code, false, request.LockDateUnix, request.Note, false, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=fiscal_set_lock");
}

public sealed record ErpFiscalSetLockRequest(long LockDateUnix = 0, string? Note = null, bool ConfirmWrites = false);

public sealed record ErpFiscalSetLockDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long LockDateUnix, string? Note, bool Clearing,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { lock_date_unix = LockDateUnix, note = Note, clearing = Clearing },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
