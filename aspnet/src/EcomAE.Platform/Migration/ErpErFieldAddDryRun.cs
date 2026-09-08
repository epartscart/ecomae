namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>er_field_add</c> / <c>epc_er_field_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpErFieldAddWriteService</c>.
/// </summary>
public interface IErpErFieldAddDryRun
{
    ErpErFieldAddDryRunResult Evaluate(ErpErFieldAddRequest request);
}

public sealed class ErpErFieldAddDryRun : IErpErFieldAddDryRun
{
    public ErpErFieldAddDryRunResult Evaluate(ErpErFieldAddRequest request)
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

        var label = (request.Label ?? string.Empty).Trim();
        var key = (request.SourceKey ?? request.Code ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpErFieldAddWriteService.Validate(label, key);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpErFieldAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.FormatId, label,
            ["INSERT `epc_er_field` (NOT executed)"],
            "ErpErFieldAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_elec_reporting.php");
    }

    private static ErpErFieldAddDryRunResult Refuse(string s, string c, string d, ErpErFieldAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.FormatId, r.Label, [], d, "content/shop/finance/epc_erp_elec_reporting.php");
}

public sealed record ErpErFieldAddRequest(
    long FormatId = 0,
    string? Label = null,
    string? SourceKey = null,
    string? Code = null,
    int Ordinal = 0,
    bool ConfirmWrites = false);

public sealed record ErpErFieldAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long FormatId, string? Label,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "er_field_add", format_id = FormatId, label = Label },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
