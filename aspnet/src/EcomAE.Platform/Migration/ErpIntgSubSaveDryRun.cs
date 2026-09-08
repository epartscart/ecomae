namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>intg_sub_save</c> / <c>epc_intg_sub_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpIntgSubSaveWriteService</c>.
/// </summary>
public interface IErpIntgSubSaveDryRun
{
    ErpIntgSubSaveDryRunResult Evaluate(ErpIntgSubSaveRequest request);
}

public sealed class ErpIntgSubSaveDryRun : IErpIntgSubSaveDryRun
{
    public ErpIntgSubSaveDryRunResult Evaluate(ErpIntgSubSaveRequest request)
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

        var type = (request.TargetType ?? "webhook").Trim();
        var invalid = EcomAE.Platform.Erp.ErpIntgSubSaveWriteService.Validate(type);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpIntgSubSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Event, type,
            ["INSERT `epc_intg_event_sub` (NOT executed)"],
            "ErpIntgSubSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_integration.php");
    }

    private static ErpIntgSubSaveDryRunResult Refuse(string s, string c, string d, ErpIntgSubSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Event, r.TargetType, [], d, "content/shop/finance/epc_erp_integration.php");
}

public sealed record ErpIntgSubSaveRequest(
    string? Event = null,
    string? TargetType = null,
    string? Target = null,
    long CompanyId = 0,
    bool ConfirmWrites = false);

public sealed record ErpIntgSubSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Event, string? TargetType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "intg_sub_save", @event = Event, target_type = TargetType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
