namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>proc_req_add_line</c> / <c>epc_proc_req_add_line</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpProcurementReqAddLineWriteService</c>.
/// </summary>
public interface IErpProcReqAddLineDryRun
{
    ErpProcReqAddLineDryRunResult Evaluate(ErpProcReqAddLineRequest request);
}

public sealed class ErpProcReqAddLineDryRun : IErpProcReqAddLineDryRun
{
    public ErpProcReqAddLineDryRunResult Evaluate(ErpProcReqAddLineRequest request)
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

        if (request.Id <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "id must be positive.", request);
        }

        return new ErpProcReqAddLineDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["INSERT `epc_proc_req_line` + recalc `epc_proc_req` (NOT executed)"],
            "ErpProcReqAddLine payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_procurement.php");
    }

    private static ErpProcReqAddLineDryRunResult Refuse(string s, string c, string d, ErpProcReqAddLineRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, [], d, "content/shop/finance/epc_erp_procurement.php");
}

public sealed record ErpProcReqAddLineRequest(
    long Id,
    bool ConfirmWrites = false,
    long CategoryId = 0,
    string? ItemCode = null,
    string? Description = null,
    decimal Qty = 0,
    decimal UnitPrice = 0,
    string? PreferredVendor = null);

public sealed record ErpProcReqAddLineDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
