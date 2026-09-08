namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>qm_test_add</c> / <c>epc_qm_test_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpQmTestAddWriteService</c>.
/// </summary>
public interface IErpQmTestAddDryRun
{
    ErpQmTestAddDryRunResult Evaluate(ErpQmTestAddRequest request);
}

public sealed class ErpQmTestAddDryRun : IErpQmTestAddDryRun
{
    public ErpQmTestAddDryRunResult Evaluate(ErpQmTestAddRequest request)
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

        return new ErpQmTestAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.PlanId, request.Name,
            ["INSERT `epc_qm_test` (NOT executed)"],
            "ErpQmTestAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_quality.php");
    }

    private static ErpQmTestAddDryRunResult Refuse(string s, string c, string d, ErpQmTestAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.PlanId, r.Name, [], d, "content/shop/finance/epc_erp_quality.php");
}

public sealed record ErpQmTestAddRequest(
    long PlanId = 0,
    string? Name = null,
    string? TestType = null,
    string? Unit = null,
    decimal? MinVal = null,
    decimal? MaxVal = null,
    string? Expected = null,
    int Sort = 0,
    bool ConfirmWrites = false);

public sealed record ErpQmTestAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PlanId, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "qm_test_add", plan_id = PlanId, name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
