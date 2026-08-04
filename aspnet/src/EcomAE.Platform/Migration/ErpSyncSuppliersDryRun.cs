namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>sync_suppliers</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpSyncSuppliersDryRun
{
    ErpSyncSuppliersDryRunResult Evaluate(ErpSyncSuppliersRequest request);
}

public sealed class ErpSyncSuppliersDryRun : IErpSyncSuppliersDryRun
{
    public ErpSyncSuppliersDryRunResult Evaluate(ErpSyncSuppliersRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return new ErpSyncSuppliersDryRunResult(
                "dry-run-confirm-refused", 0, true, false, true, "confirm_writes_refused", false,
                [], "confirm_writes requested but live ASP.NET sync_suppliers is not implemented; PHP ajax_erp.php remains authoritative.",
                "/CP/content/shop/finance/erp/ajax_erp.php?action=sync_suppliers");
        }

        return new ErpSyncSuppliersDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            [
                "epc_erp_sync_suppliers_from_storages(@db) warehouse→supplier upsert (NOT executed)"
            ],
            "Supplier sync from warehouses simulated; INSERT/UPDATE blocked until dual-sample.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=sync_suppliers");
    }
}

public sealed record ErpSyncSuppliersRequest(bool ConfirmWrites = false);

public sealed record ErpSyncSuppliersDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "sync_suppliers_from_storages" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
