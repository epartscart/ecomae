namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>intg_entity_save</c> / <c>epc_intg_entity_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpIntgEntitySaveWriteService</c>.
/// </summary>
public interface IErpIntgEntitySaveDryRun
{
    ErpIntgEntitySaveDryRunResult Evaluate(ErpIntgEntitySaveRequest request);
}

public sealed class ErpIntgEntitySaveDryRun : IErpIntgEntitySaveDryRun
{
    public ErpIntgEntitySaveDryRunResult Evaluate(ErpIntgEntitySaveRequest request)
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

        var name = (request.Name ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpIntgEntitySaveWriteService.Validate(name);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpIntgEntitySaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CompanyId, name,
            ["INSERT/UPDATE `epc_intg_entity` (NOT executed)"],
            "ErpIntgEntitySave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_integration.php");
    }

    private static ErpIntgEntitySaveDryRunResult Refuse(string s, string c, string d, ErpIntgEntitySaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.CompanyId, r.Name, [], d, "content/shop/finance/epc_erp_integration.php");
}

public sealed record ErpIntgEntitySaveRequest(
    long CompanyId = 0,
    string? Name = null,
    string? SourceTable = null,
    string? KeyField = null,
    string? Fields = null,
    int? Enabled = null,
    bool ConfirmWrites = false);

public sealed record ErpIntgEntitySaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CompanyId, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "intg_entity_save", company_id = CompanyId, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
