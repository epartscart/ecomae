namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>mfg_wo_create</c> / <c>epc_mfg_wo_create</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpMfgWoCreateWriteService</c>.
/// </summary>
public interface IErpMfgWoCreateDryRun
{
    ErpMfgWoCreateDryRunResult Evaluate(ErpMfgWoCreateRequest request);
}

public sealed class ErpMfgWoCreateDryRun : IErpMfgWoCreateDryRun
{
    public ErpMfgWoCreateDryRunResult Evaluate(ErpMfgWoCreateRequest request)
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

        if (request.BomId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "BOM not found", request);
        }

        return new ErpMfgWoCreateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.BomId, request.WoNo,
            ["INSERT `epc_mfg_work_orders` (NOT executed)"],
            "ErpMfgWoCreate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_manufacturing.php");
    }

    private static ErpMfgWoCreateDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpMfgWoCreateRequest request) =>
        new(status, 0, true, false, false, code, false, request.BomId, request.WoNo, [], detail,
            "content/shop/finance/epc_erp_manufacturing.php");
}

public sealed record ErpMfgWoCreateRequest(
    long BomId = 0,
    string? WoNo = null,
    bool ConfirmWrites = false);

public sealed record ErpMfgWoCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long BomId, string? WoNo,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { bomId = BomId, woNo = WoNo, action = "mfg_wo_create" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
