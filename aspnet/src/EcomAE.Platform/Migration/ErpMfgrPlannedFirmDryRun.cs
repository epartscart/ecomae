namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>mfgr_planned_firm</c> / <c>epc_mfgr_planned_firm</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpMfgrPlannedFirmWriteService</c>.
/// </summary>
public interface IErpMfgrPlannedFirmDryRun
{
    ErpMfgrPlannedFirmDryRunResult Evaluate(ErpMfgrPlannedFirmRequest request);
}

public sealed class ErpMfgrPlannedFirmDryRun : IErpMfgrPlannedFirmDryRun
{
    public ErpMfgrPlannedFirmDryRunResult Evaluate(ErpMfgrPlannedFirmRequest request)
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

        return new ErpMfgrPlannedFirmDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["ajax_erp.php?action=mfgr_planned_firm (NOT executed)"],
            "ErpMfgrPlannedFirm payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=mfgr_planned_firm");
    }

    private static ErpMfgrPlannedFirmDryRunResult Refuse(string s, string c, string d, ErpMfgrPlannedFirmRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=mfgr_planned_firm");
}

public sealed record ErpMfgrPlannedFirmRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpMfgrPlannedFirmDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "mfgr_planned_firm", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
