namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pm_listing_attach</c> / <c>epc_erp_pm_listing_attach</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpPmListingAttachWriteService</c>.
/// </summary>
public interface IErpPmListingAttachDryRun
{
    ErpPmListingAttachDryRunResult Evaluate(ErpPmListingAttachRequest request);
}

public sealed class ErpPmListingAttachDryRun : IErpPmListingAttachDryRun
{
    public ErpPmListingAttachDryRunResult Evaluate(ErpPmListingAttachRequest request)
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

        if (request.Id < 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "id must be >= 0.", request);
        }

        return new ErpPmListingAttachDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["ajax_erp.php?action=pm_listing_attach (NOT executed)"],
            "ErpPmListingAttach payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=pm_listing_attach");
    }

    private static ErpPmListingAttachDryRunResult Refuse(string s, string c, string d, ErpPmListingAttachRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=pm_listing_attach");
}

public sealed record ErpPmListingAttachRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpPmListingAttachDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "pm_listing_attach", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
