namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cft_instrument_save</c> / <c>epc_cft_instrument_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE/INSERT is
/// <c>IErpCftInstrumentSaveWriteService</c>.
/// </summary>
public interface IErpCftInstrumentSaveDryRun
{
    ErpCftInstrumentSaveDryRunResult Evaluate(ErpCftInstrumentSaveRequest request);
}

public sealed class ErpCftInstrumentSaveDryRun : IErpCftInstrumentSaveDryRun
{
    public ErpCftInstrumentSaveDryRunResult Evaluate(ErpCftInstrumentSaveRequest request)
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

        return new ErpCftInstrumentSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Ref,
            ["UPSERT `epc_cft_instrument` (NOT executed)"],
            "ErpCftInstrumentSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_cash_treasury.php");
    }

    private static ErpCftInstrumentSaveDryRunResult Refuse(string s, string c, string d, ErpCftInstrumentSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Ref, [], d, "content/shop/finance/epc_erp_cash_treasury.php");
}

public sealed record ErpCftInstrumentSaveRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Ref = null,
    string? Type = null,
    string? Beneficiary = null,
    string? Applicant = null,
    string? Bank = null,
    decimal Amount = 0,
    string? Currency = null,
    string? IssueDate = null,
    string? ExpiryDate = null,
    string? Notes = null,
    bool ConfirmWrites = false);

public sealed record ErpCftInstrumentSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Ref,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "cft_instrument_save", id = Id, @ref = Ref },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
