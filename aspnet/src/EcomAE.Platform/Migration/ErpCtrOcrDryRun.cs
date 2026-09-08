namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>ctr_ocr</c> / <c>epc_ctr_ocr_store</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpCtrOcrWriteService</c>.
/// </summary>
public interface IErpCtrOcrDryRun
{
    ErpCtrOcrDryRunResult Evaluate(ErpCtrOcrRequest request);
}

public sealed class ErpCtrOcrDryRun : IErpCtrOcrDryRun
{
    public ErpCtrOcrDryRunResult Evaluate(ErpCtrOcrRequest request)
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

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "id must be >= 0.", request);
        }

        return new ErpCtrOcrDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["ajax_erp.php?action=ctr_ocr (NOT executed)"],
            "ErpCtrOcr payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_ocr");
    }

    private static ErpCtrOcrDryRunResult Refuse(string s, string c, string d, ErpCtrOcrRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=ctr_ocr");
}

public sealed record ErpCtrOcrRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpCtrOcrDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "ctr_ocr", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
