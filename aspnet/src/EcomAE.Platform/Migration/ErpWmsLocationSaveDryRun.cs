namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>wms_location_save</c>. Never INSERT/UPDATE. PHP authoritative.</summary>
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
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET wms_location_save is not implemented; PHP ajax_erp.php remains authoritative.", request);

        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
            return Refuse("dry-run-invalid", "code_required", "Location code is required.", request);

        return new ErpWmsLocationSaveDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, code, request.Id,
            ["epc_wms_location_save(@data, @id) (NOT executed)"],
            "WMS location save payload validated; write blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_location_save");
    }

    private static ErpWmsLocationSaveDryRunResult Refuse(string status, string code, string detail, ErpWmsLocationSaveRequest request) =>
        new(status, 0, true, false, true, code, false, request.Code, request.Id, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=wms_location_save");
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
