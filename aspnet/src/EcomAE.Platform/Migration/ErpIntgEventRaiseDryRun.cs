namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>intg_event_raise</c> / <c>epc_intg_event_raise</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpIntgEventRaiseWriteService</c>.
/// </summary>
public interface IErpIntgEventRaiseDryRun
{
    ErpIntgEventRaiseDryRunResult Evaluate(ErpIntgEventRaiseRequest request);
}

public sealed class ErpIntgEventRaiseDryRun : IErpIntgEventRaiseDryRun
{
    public ErpIntgEventRaiseDryRunResult Evaluate(ErpIntgEventRaiseRequest request)
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

        return new ErpIntgEventRaiseDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Event, request.CompanyId,
            ["INSERT `epc_intg_event_log` (NOT executed)"],
            "ErpIntgEventRaise payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_integration.php");
    }

    private static ErpIntgEventRaiseDryRunResult Refuse(string s, string c, string d, ErpIntgEventRaiseRequest r) =>
        new(s, 0, true, false, false, c, false, r.Event, r.CompanyId, [], d, "content/shop/finance/epc_erp_integration.php");
}

public sealed record ErpIntgEventRaiseRequest(
    string? Event = null,
    string? Payload = null,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpIntgEventRaiseDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Event, long CompanyId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "intg_event_raise", @event = Event, company_id = CompanyId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
