namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>wms_wave_create</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT is <c>IErpWmsWaveCreateWriteService</c>.
/// </summary>
public interface IErpWmsWaveCreateDryRun
{
    ErpWmsWaveCreateDryRunResult Evaluate(ErpWmsWaveCreateRequest request);
}

public sealed class ErpWmsWaveCreateDryRun : IErpWmsWaveCreateDryRun
{
    public ErpWmsWaveCreateDryRunResult Evaluate(ErpWmsWaveCreateRequest request)
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
        if (item.Length == 0 || request.Qty <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "item and positive qty required for pick work.", request);
        }

        return new(
            "dry-run-validated",
            0,
            true,
            false,
            false,
            "ok",
            true,
            item,
            request.Qty,
            request.Reference,
            ["epc_wms_wave_create + epc_wms_wave_add_pick (NOT executed)"],
            "ErpWmsWaveCreate payload validated; INSERT blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_wms.php");
    }

    private static ErpWmsWaveCreateDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpWmsWaveCreateRequest request) =>
        new(status, 0, true, false, false, code, false, request.Item, request.Qty, request.Reference, [], detail,
            "content/shop/finance/epc_erp_wms.php");
}

public sealed record ErpWmsWaveCreateRequest(string? Item, decimal Qty, string? Reference = null, bool ConfirmWrites = false);

public sealed record ErpWmsWaveCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Item, decimal Qty, string? Reference,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { item = Item, qty = Qty, reference = Reference },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
