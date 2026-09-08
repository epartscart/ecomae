using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>prj_save</c> / <c>epc_prj_save</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is <c>IErpPrjSaveWriteService</c>.
/// </summary>
public interface IErpPrjSaveDryRun
{
    ErpPrjSaveDryRunResult Evaluate(ErpPrjSaveRequest request);
}

public sealed class ErpPrjSaveDryRun : IErpPrjSaveDryRun
{
    public ErpPrjSaveDryRunResult Evaluate(ErpPrjSaveRequest request)
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

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Project code is required", request);
        }

        return new ErpPrjSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.Code,
            ["INSERT/UPDATE `epc_prj_projects` (NOT executed)"],
            "ErpPrjSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_projects.php");
    }

    private static ErpPrjSaveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPrjSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.Code, [], detail,
            "content/shop/finance/epc_erp_projects.php");
}

public sealed record ErpPrjSaveRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false);

public sealed record ErpPrjSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, code = Code, action = "prj_save" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
