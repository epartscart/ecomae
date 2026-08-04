namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>as_rma_create</c>. Never INSERT. PHP authoritative.
/// Blockchain BOS proof attachment stays PHP.
/// </summary>
public interface IErpAsRmaCreateDryRun
{
    ErpAsRmaCreateDryRunResult Evaluate(ErpAsRmaCreateRequest request);
}

public sealed class ErpAsRmaCreateDryRun : IErpAsRmaCreateDryRun
{
    public ErpAsRmaCreateDryRunResult Evaluate(ErpAsRmaCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET as_rma_create is not implemented; PHP ajax_erp.php remains authoritative.",
                request);
        }

        var lines = request.Lines ?? [];
        if (lines.Count == 0)
        {
            return Refuse("dry-run-invalid", "lines_required",
                "Add at least one return line (item_id,qty,...) (PHP).", request);
        }

        foreach (var line in lines)
        {
            if (line.ItemId <= 0 || line.Qty <= 0)
            {
                return Refuse("dry-run-invalid", "invalid_line",
                    "Each line requires itemId > 0 and qty > 0.", request);
            }
        }

        var customerId = request.CustomerId < 0 ? 0 : request.CustomerId;
        var sourceId = request.SourceId < 0 ? 0 : request.SourceId;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var rmaNo = string.IsNullOrWhiteSpace(request.RmaNo) ? null : request.RmaNo.Trim();

        return new ErpAsRmaCreateDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            CustomerId: customerId,
            SourceId: sourceId,
            RmaNo: rmaNo,
            Reason: reason,
            Restock: request.Restock,
            LineCount: lines.Count,
            SimulatedSql:
            [
                "INSERT INTO `epc_as_rma` (…) (NOT executed)",
                "INSERT INTO `epc_as_rma_lines` (…) × N (NOT executed)",
                "Optional RMA-{id} renumber + blockchain BOS proof stay PHP"
            ],
            Detail: "RMA header+lines payload validated; INSERT blocked. Blockchain proof + restock inventory stay PHP until dual-sample.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=as_rma_create");
    }

    private static ErpAsRmaCreateDryRunResult Refuse(
        string status, string code, string detail, ErpAsRmaCreateRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.CustomerId, request.SourceId, request.RmaNo, request.Reason, request.Restock,
            request.Lines?.Count ?? 0, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=as_rma_create");
}

public sealed record ErpAsRmaCreateLine(long ItemId, decimal Qty, decimal UnitPrice = 0, string? ConditionNote = null);

public sealed record ErpAsRmaCreateRequest(
    long CustomerId,
    long SourceId = 0,
    string? RmaNo = null,
    string? Reason = null,
    bool Restock = false,
    IReadOnlyList<ErpAsRmaCreateLine>? Lines = null,
    bool ConfirmWrites = false);

public sealed record ErpAsRmaCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CustomerId, long SourceId, string? RmaNo,
    string? Reason, bool Restock, int LineCount,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new
        {
            customer_id = CustomerId,
            source_type = "sales_order",
            source_id = SourceId,
            rma_no = RmaNo,
            reason = Reason,
            restock = Restock,
            line_count = LineCount
        },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
