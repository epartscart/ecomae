namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>oa_contact_save</c> / <c>epc_oa_contact_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpOaContactSaveWriteService</c>.
/// </summary>
public interface IErpOaContactSaveDryRun
{
    ErpOaContactSaveDryRunResult Evaluate(ErpOaContactSaveRequest request);
}

public sealed class ErpOaContactSaveDryRun : IErpOaContactSaveDryRun
{
    public ErpOaContactSaveDryRunResult Evaluate(ErpOaContactSaveRequest request)
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

        var type = request.ContactType ?? "email";
        var invalid = EcomAE.Platform.Erp.ErpOaContactSaveWriteService.Validate(type);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpOaContactSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.PartyId, type,
            ["INSERT `epc_oa_contact` (NOT executed)"],
            "ErpOaContactSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_orgadmin.php");
    }

    private static ErpOaContactSaveDryRunResult Refuse(string s, string c, string d, ErpOaContactSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.PartyId, r.ContactType, [], d, "content/shop/finance/epc_erp_orgadmin.php");
}

public sealed record ErpOaContactSaveRequest(
    long PartyId = 0,
    string? ContactType = null,
    string? Value = null,
    int? IsPrimary = null,
    bool ConfirmWrites = false);

public sealed record ErpOaContactSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PartyId, string? ContactType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "oa_contact_save", party_id = PartyId, contact_type = ContactType },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
