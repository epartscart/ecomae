namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>wht_record</c> / <c>epc_wht_record</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpWhtRecordWriteService</c>.
/// </summary>
public interface IErpWhtRecordDryRun
{
    ErpWhtRecordDryRunResult Evaluate(ErpWhtRecordRequest request);
}

public sealed class ErpWhtRecordDryRun : IErpWhtRecordDryRun
{
    public ErpWhtRecordDryRunResult Evaluate(ErpWhtRecordRequest request)
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

        var invalid = EcomAE.Platform.Erp.ErpWhtRecordWriteService.Validate(request.CodeId, request.BaseAmount);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpWhtRecordDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CodeId, request.Code,
            ["INSERT `epc_wht_txn` (NOT executed)"],
            "ErpWhtRecord payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_withholding.php");
    }

    private static ErpWhtRecordDryRunResult Refuse(string s, string c, string d, ErpWhtRecordRequest r) =>
        new(s, 0, true, false, false, c, false, r.CodeId, r.Code, [], d, "content/shop/finance/epc_erp_withholding.php");
}

public sealed record ErpWhtRecordRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    long CodeId = 0,
    decimal BaseAmount = 0,
    string? Vendor = null,
    string? DocRef = null,
    string? TxnDate = null,
    long CompanyId = 0);

public sealed record ErpWhtRecordDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "wht_record", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
