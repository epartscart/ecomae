namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cons_entity_save</c> / <c>epc_cons_entity_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpConsEntitySaveWriteService</c>.
/// </summary>
public interface IErpConsEntitySaveDryRun
{
    ErpConsEntitySaveDryRunResult Evaluate(ErpConsEntitySaveRequest request);
}

public sealed class ErpConsEntitySaveDryRun : IErpConsEntitySaveDryRun
{
    public ErpConsEntitySaveDryRunResult Evaluate(ErpConsEntitySaveRequest request)
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

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "An entity id must be >= 0.", request);
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Entity code is required", request);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Entity name is required", request);
        }

        return new ErpConsEntitySaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.Code, request.Name,
            [request.Id > 0
                ? "UPDATE `epc_cons_entities` (NOT executed)"
                : "INSERT `epc_cons_entities` (NOT executed)"],
            "ErpConsEntitySave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_consolidation.php");
    }

    private static ErpConsEntitySaveDryRunResult Refuse(string status, string code, string detail, ErpConsEntitySaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.Code, request.Name, [], detail,
            "content/shop/finance/epc_erp_consolidation.php");
}

public sealed record ErpConsEntitySaveRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    bool ConfirmWrites = false);

public sealed record ErpConsEntitySaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, code = Code, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
