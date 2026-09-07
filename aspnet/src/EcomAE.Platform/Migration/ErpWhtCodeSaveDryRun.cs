namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>wht_code_save</c> / <c>epc_wht_code_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpWhtCodeSaveWriteService</c>.
/// </summary>
public interface IErpWhtCodeSaveDryRun
{
    ErpWhtCodeSaveDryRunResult Evaluate(ErpWhtCodeSaveRequest request);
}

public sealed class ErpWhtCodeSaveDryRun : IErpWhtCodeSaveDryRun
{
    public ErpWhtCodeSaveDryRunResult Evaluate(ErpWhtCodeSaveRequest request)
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
        var name = (request.Name ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpWhtCodeSaveWriteService.Validate(code, name, request.Rate);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpWhtCodeSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["INSERT/UPDATE `epc_wht_code` (NOT executed)"],
            "ErpWhtCodeSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_withholding.php");
    }

    private static ErpWhtCodeSaveDryRunResult Refuse(string s, string c, string d, ErpWhtCodeSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_withholding.php");
}

public sealed record ErpWhtCodeSaveRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    string? Name = null,
    decimal Rate = 0,
    string? Account = null,
    int? Active = null,
    long CompanyId = 0);

public sealed record ErpWhtCodeSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "wht_code_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
