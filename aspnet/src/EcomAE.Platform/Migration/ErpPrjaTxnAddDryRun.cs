namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>prja_txn_add</c> / <c>epc_prja_txn_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpPrjaTxnAddWriteService</c>.
/// </summary>
public interface IErpPrjaTxnAddDryRun
{
    ErpPrjaTxnAddDryRunResult Evaluate(ErpPrjaTxnAddRequest request);
}

public sealed class ErpPrjaTxnAddDryRun : IErpPrjaTxnAddDryRun
{
    public ErpPrjaTxnAddDryRunResult Evaluate(ErpPrjaTxnAddRequest request)
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

        var type = request.TxnType ?? "cost";
        var invalid = EcomAE.Platform.Erp.ErpPrjaTxnAddWriteService.Validate(type);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpPrjaTxnAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ProjectId, type,
            ["INSERT `epc_prja_txn` (NOT executed)"],
            "ErpPrjaTxnAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_project_accounting.php");
    }

    private static ErpPrjaTxnAddDryRunResult Refuse(string s, string c, string d, ErpPrjaTxnAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.ProjectId, r.TxnType, [], d, "content/shop/finance/epc_erp_project_accounting.php");
}

public sealed record ErpPrjaTxnAddRequest(
    long ProjectId = 0,
    string? TxnType = null,
    string? Category = null,
    string? Description = null,
    decimal Amount = 0,
    long CompanyId = 0,
    long TxnDate = 0,
    bool ConfirmWrites = false);

public sealed record ErpPrjaTxnAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ProjectId, string? TxnType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "prja_txn_add", project_id = ProjectId, txn_type = TxnType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
