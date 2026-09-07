namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>plt_feature_save</c> / <c>epc_plt_feature_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpPltFeatureSaveWriteService</c>.
/// </summary>
public interface IErpPltFeatureSaveDryRun
{
    ErpPltFeatureSaveDryRunResult Evaluate(ErpPltFeatureSaveRequest request);
}

public sealed class ErpPltFeatureSaveDryRun : IErpPltFeatureSaveDryRun
{
    public ErpPltFeatureSaveDryRunResult Evaluate(ErpPltFeatureSaveRequest request)
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

        var code = (request.Code ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpPltFeatureSaveWriteService.Validate(code);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpPltFeatureSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CompanyId, code,
            ["INSERT/UPDATE `epc_plt_feature` (NOT executed)"],
            "ErpPltFeatureSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_platform.php");
    }

    private static ErpPltFeatureSaveDryRunResult Refuse(string s, string c, string d, ErpPltFeatureSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.CompanyId, r.Code, [], d, "content/shop/finance/epc_erp_platform.php");
}

public sealed record ErpPltFeatureSaveRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    int? Enabled = null,
    bool ConfirmWrites = false);

public sealed record ErpPltFeatureSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CompanyId, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "plt_feature_save", company_id = CompanyId, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
