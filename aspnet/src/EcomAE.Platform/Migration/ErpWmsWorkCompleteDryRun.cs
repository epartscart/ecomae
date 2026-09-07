namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>wms_work_complete</c> / <c>epc_wms_work_complete</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpWmsWorkCompleteWriteService</c>.
/// </summary>
public interface IErpWmsWorkCompleteDryRun
{
    ErpWmsWorkCompleteDryRunResult Evaluate(ErpWmsWorkCompleteRequest request);
}

public sealed class ErpWmsWorkCompleteDryRun : IErpWmsWorkCompleteDryRun
{
    public ErpWmsWorkCompleteDryRunResult Evaluate(ErpWmsWorkCompleteRequest request)
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

        return new ErpWmsWorkCompleteDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["epc_wms_work_complete(@id) (NOT executed)"],
            "ErpWmsWorkComplete payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_wms.php");
    }

    private static ErpWmsWorkCompleteDryRunResult Refuse(string s, string c, string d, ErpWmsWorkCompleteRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, [], d, "content/shop/finance/epc_erp_wms.php");
}

public sealed record ErpWmsWorkCompleteRequest(long Id, bool ConfirmWrites = false);

public sealed record ErpWmsWorkCompleteDryRunResult(
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
