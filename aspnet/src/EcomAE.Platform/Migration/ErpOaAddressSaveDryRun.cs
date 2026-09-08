namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>oa_address_save</c> / <c>epc_oa_address_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpOaAddressSaveWriteService</c>.
/// </summary>
public interface IErpOaAddressSaveDryRun
{
    ErpOaAddressSaveDryRunResult Evaluate(ErpOaAddressSaveRequest request);
}

public sealed class ErpOaAddressSaveDryRun : IErpOaAddressSaveDryRun
{
    public ErpOaAddressSaveDryRunResult Evaluate(ErpOaAddressSaveRequest request)
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

        var purpose = request.Purpose ?? "business";
        var invalid = EcomAE.Platform.Erp.ErpOaAddressSaveWriteService.Validate(purpose);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpOaAddressSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.PartyId, purpose,
            ["INSERT `epc_oa_address` (NOT executed)"],
            "ErpOaAddressSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_orgadmin.php");
    }

    private static ErpOaAddressSaveDryRunResult Refuse(string s, string c, string d, ErpOaAddressSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.PartyId, r.Purpose, [], d, "content/shop/finance/epc_erp_orgadmin.php");
}

public sealed record ErpOaAddressSaveRequest(
    long PartyId = 0,
    string? Purpose = null,
    string? Line1 = null,
    string? City = null,
    string? State = null,
    string? Postcode = null,
    string? Country = null,
    int? IsPrimary = null,
    bool ConfirmWrites = false);

public sealed record ErpOaAddressSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PartyId, string? Purpose,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "oa_address_save", party_id = PartyId, purpose = Purpose },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
