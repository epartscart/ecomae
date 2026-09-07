namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>kb_save</c> / <c>epc_erp_kb_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpKbSaveWriteService</c>.
/// </summary>
public interface IErpKbSaveDryRun
{
    ErpKbSaveDryRunResult Evaluate(ErpKbSaveRequest request);
}

public sealed class ErpKbSaveDryRun : IErpKbSaveDryRun
{
    public ErpKbSaveDryRunResult Evaluate(ErpKbSaveRequest request)
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

        var title = (request.Title ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpKbSaveWriteService.Validate(title);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpKbSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, title,
            ["INSERT `epc_erp_kb_articles` (NOT executed)"],
            "ErpKbSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_extended.php");
    }

    private static ErpKbSaveDryRunResult Refuse(string s, string c, string d, ErpKbSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Title, [], d, "content/shop/finance/epc_erp_extended.php");
}

public sealed record ErpKbSaveRequest(
    string? Title = null,
    string? Category = null,
    string? Summary = null,
    string? BodyHtml = null,
    bool ConfirmWrites = false);

public sealed record ErpKbSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Title,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "kb_save", title = Title },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
