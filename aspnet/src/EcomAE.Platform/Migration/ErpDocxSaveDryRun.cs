namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>docx_save</c> / <c>epc_docx_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpDocxSaveWriteService</c>.
/// </summary>
public interface IErpDocxSaveDryRun
{
    ErpDocxSaveDryRunResult Evaluate(ErpDocxSaveRequest request);
}

public sealed class ErpDocxSaveDryRun : IErpDocxSaveDryRun
{
    public ErpDocxSaveDryRunResult Evaluate(ErpDocxSaveRequest request)
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

        return new ErpDocxSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.DocType,
            ["UPSERT `epc_erp_doc_expiry` (NOT executed)"],
            "ErpDocxSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_doc_expiry.php");
    }

    private static ErpDocxSaveDryRunResult Refuse(string s, string c, string d, ErpDocxSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.DocType, [], d, "content/shop/finance/epc_erp_doc_expiry.php");
}

public sealed record ErpDocxSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Category = null,
    string? DocType = null,
    string? Title = null,
    string? RefNo = null,
    string? Owner = null,
    string? OwnerEmail = null,
    string? Issuer = null,
    string? IssueDateStr = null,
    string? ExpiryDateStr = null,
    long IssueDate = 0,
    long ExpiryDate = 0,
    string? ReminderDays = null,
    string? AttachmentPath = null,
    string? Note = null,
    string? SourceModule = null,
    long SourceRefId = 0,
    int? Active = null,
    bool ConfirmWrites = false);

public sealed record ErpDocxSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? DocType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "docx_save", id = Id, doc_type = DocType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
