namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>er_format_save</c> / <c>epc_er_format_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpErFormatSaveWriteService</c>.
/// </summary>
public interface IErpErFormatSaveDryRun
{
    ErpErFormatSaveDryRunResult Evaluate(ErpErFormatSaveRequest request);
}

public sealed class ErpErFormatSaveDryRun : IErpErFormatSaveDryRun
{
    public ErpErFormatSaveDryRunResult Evaluate(ErpErFormatSaveRequest request)
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
        var type = string.IsNullOrWhiteSpace(request.OutputType) ? "csv" : request.OutputType.Trim();
        var invalid = EcomAE.Platform.Erp.ErpErFormatSaveWriteService.Validate(code, name, type);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpErFormatSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["INSERT/UPDATE `epc_er_format` (NOT executed)"],
            "ErpErFormatSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_elec_reporting.php");
    }

    private static ErpErFormatSaveDryRunResult Refuse(string s, string c, string d, ErpErFormatSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_elec_reporting.php");
}

public sealed record ErpErFormatSaveRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    string? OutputType = null,
    string? RootElement = null,
    string? RowElement = null,
    int? Active = null,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpErFormatSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "er_format_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
