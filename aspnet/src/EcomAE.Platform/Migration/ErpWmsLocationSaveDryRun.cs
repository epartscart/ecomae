namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_wms_location_save</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT/UPDATE is <c>IErpWmsLocationWriteService</c>.
/// </summary>
public interface IErpWmsLocationSaveDryRun
{
    ErpWmsLocationSaveDryRunResult Evaluate(ErpWmsLocationSaveRequest request);
}

public sealed class ErpWmsLocationSaveDryRun : IErpWmsLocationSaveDryRun
{
    public ErpWmsLocationSaveDryRunResult Evaluate(ErpWmsLocationSaveRequest request)
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

        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return Refuse("dry-run-invalid", "code_required", "Location code is required.", request);
        }

        return new ErpWmsLocationSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, code, request.Id,
            ["INSERT/UPDATE `epc_erp_wms_locations` (NOT executed)"],
            "ErpWmsLocationSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_wms.php");
    }

    private static ErpWmsLocationSaveDryRunResult Refuse(string status, string code, string detail, ErpWmsLocationSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Code, request.Id, [], detail,
            "content/shop/finance/epc_erp_wms.php");
}

public sealed record ErpWmsLocationSaveRequest(string? Code, long Id = 0, bool ConfirmWrites = false);
public sealed record ErpWmsLocationSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { code = Code, id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
