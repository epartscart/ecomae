namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>wms_receive</c>. Never INSERT. PHP authoritative.</summary>
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
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET wms_receive is not implemented; PHP ajax_erp.php remains authoritative.", request);

        var item = (request.Item ?? string.Empty).Trim();
        if (item.Length == 0)
            return Refuse("dry-run-invalid", "item_required", "Item is required (PHP).", request);
        if (request.Qty <= 0)
            return Refuse("dry-run-invalid", "qty_required", "qty must be positive.", request);

        return new ErpWmsReceiveDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true,
            item, request.Qty, request.ReceiveLocationId, request.PutawayLocationId,
            ["epc_wms_receive(@company, @item, @qty, @recvLoc, @putLoc, …) (NOT executed)"],
            "WMS receive payload validated; put-away work INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_receive");
    }

    private static ErpWmsReceiveDryRunResult Refuse(string status, string code, string detail, ErpWmsReceiveRequest request) =>
        new(status, 0, true, false, true, code, false, request.Item, request.Qty, request.ReceiveLocationId, request.PutawayLocationId, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_receive");
}

public sealed record ErpWmsReceiveRequest(
    string? Item, decimal Qty, long ReceiveLocationId = 0, long PutawayLocationId = 0, bool ConfirmWrites = false);
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
