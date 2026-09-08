namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>mfg_bom_save</c> / <c>epc_mfg_bom_save</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT/UPDATE + line replace is
/// <c>IErpMfgBomSaveWriteService</c>.
/// </summary>
public interface IErpMfgBomSaveDryRun
{
    ErpMfgBomSaveDryRunResult Evaluate(ErpMfgBomSaveRequest request);
}

public sealed class ErpMfgBomSaveDryRun : IErpMfgBomSaveDryRun
{
    public ErpMfgBomSaveDryRunResult Evaluate(ErpMfgBomSaveRequest request)
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

        if (request.ProductItemId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select a finished product", request);
        }

        if (request.LineCount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Add at least one component", request);
        }

        return new ErpMfgBomSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.ProductItemId, request.LineCount,
            [request.Id > 0
                ? "UPDATE `epc_mfg_bom` + replace `epc_mfg_bom_lines` (NOT executed)"
                : "INSERT `epc_mfg_bom` + `epc_mfg_bom_lines` (NOT executed)"],
            "ErpMfgBomSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_manufacturing.php");
    }

    private static ErpMfgBomSaveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpMfgBomSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.ProductItemId, request.LineCount, [], detail,
            "content/shop/finance/epc_erp_manufacturing.php");
}

public sealed record ErpMfgBomSaveRequest(
    long Id = 0,
    long ProductItemId = 0,
    int LineCount = 0,
    bool ConfirmWrites = false);

public sealed record ErpMfgBomSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, long ProductItemId, int LineCount,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, productItemId = ProductItemId, lineCount = LineCount, action = "mfg_bom_save" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
