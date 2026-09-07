namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>coll_hold_set</c> / <c>epc_coll_hold_set</c>
/// when <c>confirmWrites</c> is omitted. Live write is
/// <c>IErpCollectionsHoldSetWriteService</c>.
/// </summary>
public interface IErpCollHoldSetDryRun
{
    ErpCollHoldSetDryRunResult Evaluate(ErpCollHoldSetRequest request);
}

public sealed class ErpCollHoldSetDryRun : IErpCollHoldSetDryRun
{
    public ErpCollHoldSetDryRunResult Evaluate(ErpCollHoldSetRequest request)
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

        if (request.CustomerId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "customerId must be positive.", request);
        }

        return new ErpCollHoldSetDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CustomerId,
            ["UPSERT `epc_credit_profiles.on_hold` + INSERT `epc_coll_hold` (NOT executed)"],
            "ErpCollHoldSet payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_collections.php");
    }

    private static ErpCollHoldSetDryRunResult Refuse(string s, string c, string d, ErpCollHoldSetRequest r) =>
        new(s, 0, true, false, false, c, false, r.CustomerId, [], d, "content/shop/finance/epc_erp_collections.php");
}

public sealed record ErpCollHoldSetRequest(
    long CustomerId = 0,
    bool ConfirmWrites = false,
    bool Place = true,
    string? Reason = null,
    string? Actor = null,
    long CompanyId = 0);

public sealed record ErpCollHoldSetDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CustomerId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { customer_id = CustomerId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
