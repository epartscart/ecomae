namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>docx_delete</c> / <c>epc_docx_delete</c>
/// when <c>confirmWrites</c> is omitted. Live DELETE is
/// <c>IErpDocxDeleteWriteService</c>.
/// </summary>
public interface IErpDocxDeleteDryRun
{
    ErpDocxDeleteDryRunResult Evaluate(ErpDocxDeleteRequest request);
}

public sealed class ErpDocxDeleteDryRun : IErpDocxDeleteDryRun
{
    public ErpDocxDeleteDryRunResult Evaluate(ErpDocxDeleteRequest request)
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

        return new ErpDocxDeleteDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id,
            ["DELETE `epc_erp_doc_expiry_reminders` + `epc_erp_doc_expiry` (NOT executed)"],
            "ErpDocxDelete payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_doc_expiry.php");
    }

    private static ErpDocxDeleteDryRunResult Refuse(string s, string c, string d, ErpDocxDeleteRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, [], d, "content/shop/finance/epc_erp_doc_expiry.php");
}

public sealed record ErpDocxDeleteRequest(
    long Id = 0,
    bool ConfirmWrites = false);

public sealed record ErpDocxDeleteDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "docx_delete", id = Id },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
