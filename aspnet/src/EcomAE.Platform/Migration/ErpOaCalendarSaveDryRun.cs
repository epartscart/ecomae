namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>oa_calendar_save</c> / <c>epc_oa_calendar_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpOaCalendarSaveWriteService</c>.
/// </summary>
public interface IErpOaCalendarSaveDryRun
{
    ErpOaCalendarSaveDryRunResult Evaluate(ErpOaCalendarSaveRequest request);
}

public sealed class ErpOaCalendarSaveDryRun : IErpOaCalendarSaveDryRun
{
    public ErpOaCalendarSaveDryRunResult Evaluate(ErpOaCalendarSaveRequest request)
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

        var code = (request.Code ?? string.Empty).Trim();
        var workingDays = request.WorkingDays is null ? "1,2,3,4,5" : request.WorkingDays.Trim();
        var invalid = EcomAE.Platform.Erp.ErpOaCalendarSaveWriteService.Validate(code, workingDays);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpOaCalendarSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CompanyId, code,
            ["INSERT/UPDATE `epc_oa_calendar` (NOT executed)"],
            "ErpOaCalendarSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_orgadmin.php");
    }

    private static ErpOaCalendarSaveDryRunResult Refuse(string s, string c, string d, ErpOaCalendarSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.CompanyId, r.Code, [], d, "content/shop/finance/epc_erp_orgadmin.php");
}

public sealed record ErpOaCalendarSaveRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? WorkingDays = null,
    bool ConfirmWrites = false);

public sealed record ErpOaCalendarSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CompanyId, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "oa_calendar_save", company_id = CompanyId, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
