namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cons_figures_save</c> / <c>epc_cons_figures_save</c> when
/// <c>confirmWrites</c> is omitted. Live UPSERT is <c>IErpConsFiguresSaveWriteService</c>.
/// </summary>
public interface IErpConsFiguresSaveDryRun
{
    ErpConsFiguresSaveDryRunResult Evaluate(ErpConsFiguresSaveRequest request);
}

public sealed class ErpConsFiguresSaveDryRun : IErpConsFiguresSaveDryRun
{
    public ErpConsFiguresSaveDryRunResult Evaluate(ErpConsFiguresSaveRequest request)
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

        if (string.IsNullOrWhiteSpace(request.EntityCode))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Entity is required", request);
        }

        return new ErpConsFiguresSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.EntityCode,
            ["INSERT/UPDATE `epc_cons_figures` (NOT executed)"],
            "ErpConsFiguresSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_consolidation.php");
    }

    private static ErpConsFiguresSaveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpConsFiguresSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.EntityCode, [], detail,
            "content/shop/finance/epc_erp_consolidation.php");
}

public sealed record ErpConsFiguresSaveRequest(
    string? EntityCode = null,
    bool ConfirmWrites = false);

public sealed record ErpConsFiguresSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? EntityCode,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { entityCode = EntityCode, action = "cons_figures_save" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
