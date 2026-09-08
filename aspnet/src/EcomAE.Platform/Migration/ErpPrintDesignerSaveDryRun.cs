namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>print_designer_save</c> / <c>epc_erp_print_template_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpPrintDesignerSaveWriteService</c>.
/// </summary>
public interface IErpPrintDesignerSaveDryRun
{
    ErpPrintDesignerSaveDryRunResult Evaluate(ErpPrintDesignerSaveRequest request);
}

public sealed class ErpPrintDesignerSaveDryRun : IErpPrintDesignerSaveDryRun
{
    public ErpPrintDesignerSaveDryRunResult Evaluate(ErpPrintDesignerSaveRequest request)
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

        return new ErpPrintDesignerSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPSERT `epc_erp_print_templates` (NOT executed)"],
            "ErpPrintDesignerSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_print_designer.php");
    }

    private static ErpPrintDesignerSaveDryRunResult Refuse(string s, string c, string d, ErpPrintDesignerSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_erp_print_designer.php");
}

public sealed record ErpPrintDesignerSaveRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false);

public sealed record ErpPrintDesignerSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "print_designer_save", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
