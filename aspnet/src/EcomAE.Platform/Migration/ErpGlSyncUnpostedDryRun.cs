namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>gl_sync_unposted</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpGlSyncUnpostedDryRun
{
    ErpGlSyncUnpostedDryRunResult Evaluate(ErpGlSyncUnpostedRequest request);
}

public sealed class ErpGlSyncUnpostedDryRun : IErpGlSyncUnpostedDryRun
{
    public ErpGlSyncUnpostedDryRunResult Evaluate(ErpGlSyncUnpostedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return new ErpGlSyncUnpostedDryRunResult(
                "dry-run-confirm-refused", 0, true, false, true, "confirm_writes_refused", false,
                [], "confirm_writes requested but live ASP.NET gl_sync_unposted is not implemented; PHP ajax_erp.php remains authoritative.",
                "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_sync_unposted");
        }

        return new ErpGlSyncUnpostedDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            ["epc_erp_gl_sync_unposted(@db) sub-ledger → GL journals (NOT executed)"],
            "GL sync-unposted simulated; journal INSERTs blocked until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=gl_sync_unposted");
    }
}

public sealed record ErpGlSyncUnpostedRequest(bool ConfirmWrites = false);

public sealed record ErpGlSyncUnpostedDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "gl_sync_unposted" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
