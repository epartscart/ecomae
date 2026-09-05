namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>wms_receive</c> / <c>epc_wms_receive</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpWmsReceiveWriteService</c>.
/// </summary>
public interface IErpWmsReceiveDryRun
{
    ErpWmsReceiveDryRunResult Evaluate(ErpWmsReceiveRequest request);
}

public sealed class ErpWmsReceiveDryRun : IErpWmsReceiveDryRun
{
    public ErpWmsReceiveDryRunResult Evaluate(ErpWmsReceiveRequest request)
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

        var item = (request.Item ?? string.Empty).Trim();
        if (item.Length == 0)
        {
            return Refuse("dry-run-invalid", "item_required", "Item is required", request);
        }

        if (request.Qty <= 0)
        {
            return Refuse("dry-run-invalid", "qty_required", "qty must be positive.", request);
        }

        return new ErpWmsReceiveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            item, request.Qty, request.ReceiveLocationId, request.PutawayLocationId,
            ["INSERT `epc_erp_wms_lp` + INSERT `epc_erp_wms_work` putaway (NOT executed)"],
            "ErpWmsReceive payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_wms.php");
    }

    private static ErpWmsReceiveDryRunResult Refuse(string status, string code, string detail, ErpWmsReceiveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Item, request.Qty, request.ReceiveLocationId, request.PutawayLocationId, [], detail,
            "content/shop/finance/epc_erp_wms.php");
}

public sealed record ErpWmsReceiveRequest(
    string? Item,
    decimal Qty,
    long ReceiveLocationId = 0,
    long PutawayLocationId = 0,
    bool ConfirmWrites = false,
    string? Reference = null,
    string? LpCode = null,
    long CompanyId = 0);

public sealed record ErpWmsReceiveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Item, decimal Qty, long ReceiveLocationId, long PutawayLocationId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { item = Item, qty = Qty, receive_location_id = ReceiveLocationId, putaway_location_id = PutawayLocationId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
