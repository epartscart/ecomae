namespace EcomAE.Platform.Migration;

public interface IErpItemReserveDryRun
{
    ErpItemReserveDryRunResult Evaluate(ErpItemReserveRequest request);
}

public sealed class ErpItemReserveDryRun : IErpItemReserveDryRun
{
    public ErpItemReserveDryRunResult Evaluate(ErpItemReserveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused", "Use confirmWrites=true to reserve the item.", request);
        }

        if (request.ItemId < 0 || request.Qty < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "itemId and qty must be >= 0.", request);
        }

        return new(
            "dry-run-validated",
            0,
            true,
            false,
            false,
            "ok",
            true,
            request.ItemId,
            request.Sku,
            request.Qty,
            request.SourceType,
            request.SourceId,
            "ERP item-reserve payload validated; no hold was written.",
            "/erp/ajax/item-reserve");
    }

    private static ErpItemReserveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpItemReserveRequest request)
        => new(status, 0, true, false, false, code, false, request.ItemId, request.Sku, request.Qty, request.SourceType, request.SourceId, detail, "/erp/ajax/item-reserve");
}

public sealed record ErpItemReserveRequest(
    long ItemId = 0,
    string? Sku = null,
    decimal Qty = 0,
    string? SourceType = null,
    long SourceId = 0,
    bool ConfirmWrites = false);

public sealed record ErpItemReserveDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long ItemId,
    string? Sku,
    decimal Qty,
    string? SourceType,
    long SourceId,
    string Detail,
    string Endpoint)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { itemId = ItemId, sku = Sku, qty = Qty, sourceType = SourceType, sourceId = SourceId },
        endpoint = Endpoint,
        session,
        note = Detail,
    };
}
