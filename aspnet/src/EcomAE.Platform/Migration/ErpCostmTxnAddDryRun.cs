namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>costm_txn_add</c> / <c>epc_costm_txn_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpCostmTxnAddWriteService</c>.
/// </summary>
public interface IErpCostmTxnAddDryRun
{
    ErpCostmTxnAddDryRunResult Evaluate(ErpCostmTxnAddRequest request);
}

public sealed class ErpCostmTxnAddDryRun : IErpCostmTxnAddDryRun
{
    public ErpCostmTxnAddDryRunResult Evaluate(ErpCostmTxnAddRequest request)
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

        var type = request.TxnType ?? "receipt";
        var invalid = EcomAE.Platform.Erp.ErpCostmTxnAddWriteService.Validate(type);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpCostmTxnAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ItemId, type,
            ["INSERT `epc_costm_txn` (NOT executed)"],
            "ErpCostmTxnAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_cost_models.php");
    }

    private static ErpCostmTxnAddDryRunResult Refuse(string s, string c, string d, ErpCostmTxnAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.ItemId, r.TxnType, [], d, "content/shop/finance/epc_erp_cost_models.php");
}

public sealed record ErpCostmTxnAddRequest(
    long ItemId = 0,
    string? TxnType = null,
    decimal Qty = 0,
    decimal UnitCost = 0,
    long CompanyId = 0,
    long TxnDate = 0,
    bool ConfirmWrites = false);

public sealed record ErpCostmTxnAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ItemId, string? TxnType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "costm_txn_add", item_id = ItemId, txn_type = TxnType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
