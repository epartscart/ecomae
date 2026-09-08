namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>multi_entity_save</c> / <c>epc_erp_multi_entity_set</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpMultiEntitySaveWriteService</c>.
/// </summary>
public interface IErpMultiEntitySaveDryRun
{
    ErpMultiEntitySaveDryRunResult Evaluate(ErpMultiEntitySaveRequest request);
}

public sealed class ErpMultiEntitySaveDryRun : IErpMultiEntitySaveDryRun
{
    public ErpMultiEntitySaveDryRunResult Evaluate(ErpMultiEntitySaveRequest request)
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

        return new ErpMultiEntitySaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Enabled,
            ["INSERT/UPDATE `epc_erp_platform_settings` multi_entity_enabled (NOT executed)"],
            "ErpMultiEntitySave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_extended.php");
    }

    private static ErpMultiEntitySaveDryRunResult Refuse(string s, string c, string d, ErpMultiEntitySaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Enabled, [], d, "content/shop/finance/epc_erp_extended.php");
}

public sealed record ErpMultiEntitySaveRequest(
    int? Enabled = null,
    bool ConfirmWrites = false);

public sealed record ErpMultiEntitySaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, int? Enabled,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "multi_entity_save", enabled = Enabled },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
