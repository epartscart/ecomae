namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>cft_instrument_status</c> / <c>epc_cft_instrument_set_status</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpCftInstrumentStatusWriteService</c>.
/// </summary>
public interface IErpCftInstrumentStatusDryRun
{
    ErpCftInstrumentStatusDryRunResult Evaluate(ErpCftInstrumentStatusRequest request);
}

public sealed class ErpCftInstrumentStatusDryRun : IErpCftInstrumentStatusDryRun
{
    public ErpCftInstrumentStatusDryRunResult Evaluate(ErpCftInstrumentStatusRequest request)
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

        return new ErpCftInstrumentStatusDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.TargetStatus,
            ["UPDATE `epc_cft_instrument` status (NOT executed)"],
            "ErpCftInstrumentStatus payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_cash_treasury.php");
    }

    private static ErpCftInstrumentStatusDryRunResult Refuse(string s, string c, string d, ErpCftInstrumentStatusRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.TargetStatus, [], d, "content/shop/finance/epc_erp_cash_treasury.php");
}

public sealed record ErpCftInstrumentStatusRequest(
    long Id = 0,
    string? TargetStatus = null,
    string? Detail = null,
    decimal Amount = 0,
    bool ConfirmWrites = false);

public sealed record ErpCftInstrumentStatusDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? TargetStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "cft_instrument_status", id = Id, status = TargetStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
