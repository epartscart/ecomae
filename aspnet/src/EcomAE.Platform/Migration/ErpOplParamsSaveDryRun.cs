namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>opl_params_save</c> / <c>epc_opl_params_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpOplParamsSaveWriteService</c>.
/// </summary>
public interface IErpOplParamsSaveDryRun
{
    ErpOplParamsSaveDryRunResult Evaluate(ErpOplParamsSaveRequest request);
}

public sealed class ErpOplParamsSaveDryRun : IErpOplParamsSaveDryRun
{
    public ErpOplParamsSaveDryRunResult Evaluate(ErpOplParamsSaveRequest request)
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

        return new ErpOplParamsSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ItemId, request.WarehouseId,
            ["UPSERT `epc_erp_planning_params` (NOT executed)"],
            "ErpOplParamsSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_order_planning.php");
    }

    private static ErpOplParamsSaveDryRunResult Refuse(string s, string c, string d, ErpOplParamsSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.ItemId, r.WarehouseId, [], d, "content/shop/finance/epc_erp_order_planning.php");
}

public sealed record ErpOplParamsSaveRequest(
    long ItemId = 0,
    long WarehouseId = 0,
    int? LeadTimeDays = null,
    decimal? TargetServiceLevel = null,
    int? ReviewPeriodDays = null,
    decimal? MinOrderQty = null,
    decimal? OrderMultiple = null,
    decimal? ManualBuffer = null,
    string? Supplier = null,
    int? Stocked = null,
    bool ConfirmWrites = false);

public sealed record ErpOplParamsSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ItemId, long WarehouseId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "opl_params_save", item_id = ItemId, warehouse_id = WarehouseId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
