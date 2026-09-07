namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>coll_dunning_run</c> / <c>epc_coll_dunning_run</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpCollectionsDunningRunWriteService</c>.
/// </summary>
public interface IErpCollDunningRunDryRun
{
    ErpCollDunningRunDryRunResult Evaluate(ErpCollDunningRunRequest request);
}

public sealed class ErpCollDunningRunDryRun : IErpCollDunningRunDryRun
{
    public ErpCollDunningRunDryRunResult Evaluate(ErpCollDunningRunRequest request)
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

        if (EcomAE.Platform.Erp.ErpCollectionsDunningRunWriteService.ParseCustomers(request.Customers).Count == 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "Enter at least one customer line (customerId|d1_30|d31_60|d61_90|d90_plus)",
                request);
        }

        return new ErpCollDunningRunDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            ["INSERT `epc_coll_dunning` (NOT executed)"],
            "ErpCollDunningRun payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_collections.php");
    }

    private static ErpCollDunningRunDryRunResult Refuse(string s, string c, string d, ErpCollDunningRunRequest r) =>
        new(s, 0, true, false, false, c, false, [], d, "content/shop/finance/epc_erp_collections.php");
}

public sealed record ErpCollDunningRunRequest(
    bool ConfirmWrites = false,
    string? Customers = null,
    long CompanyId = 0);

public sealed record ErpCollDunningRunDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "coll_dunning_run" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
