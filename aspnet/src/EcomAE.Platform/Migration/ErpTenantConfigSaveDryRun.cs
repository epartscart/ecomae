namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>tenant_config_save</c> / <c>epc_erp_adv_set_setting</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpTenantConfigSaveWriteService</c>.
/// </summary>
public interface IErpTenantConfigSaveDryRun
{
    ErpTenantConfigSaveDryRunResult Evaluate(ErpTenantConfigSaveRequest request);
}

public sealed class ErpTenantConfigSaveDryRun : IErpTenantConfigSaveDryRun
{
    public ErpTenantConfigSaveDryRunResult Evaluate(ErpTenantConfigSaveRequest request)
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

        return new ErpTenantConfigSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPSERT `epc_price_settings` erp_* keys (NOT executed)"],
            "ErpTenantConfigSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_advanced.php");
    }

    private static ErpTenantConfigSaveDryRunResult Refuse(string s, string c, string d, ErpTenantConfigSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_advanced.php");
}

public sealed record ErpTenantConfigSaveRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false);

public sealed record ErpTenantConfigSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "tenant_config_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
