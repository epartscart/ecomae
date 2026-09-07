namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>oa_holiday_add</c> / <c>epc_oa_holiday_add</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpOaHolidayAddWriteService</c>.
/// </summary>
public interface IErpOaHolidayAddDryRun
{
    ErpOaHolidayAddDryRunResult Evaluate(ErpOaHolidayAddRequest request);
}

public sealed class ErpOaHolidayAddDryRun : IErpOaHolidayAddDryRun
{
    public ErpOaHolidayAddDryRunResult Evaluate(ErpOaHolidayAddRequest request)
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

        var date = (request.HolidayDate ?? request.Code ?? string.Empty).Trim();
        var invalid = EcomAE.Platform.Erp.ErpOaHolidayAddWriteService.Validate(date);
        if (invalid is not null)
        {
            return Refuse("dry-run-invalid", "invalid_request", invalid, request);
        }

        return new ErpOaHolidayAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.CalendarId, date,
            ["INSERT/UPDATE `epc_oa_holiday` (NOT executed)"],
            "ErpOaHolidayAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_orgadmin.php");
    }

    private static ErpOaHolidayAddDryRunResult Refuse(string s, string c, string d, ErpOaHolidayAddRequest r) =>
        new(s, 0, true, false, false, c, false, r.CalendarId, r.HolidayDate ?? r.Code, [], d, "content/shop/finance/epc_erp_orgadmin.php");
}

public sealed record ErpOaHolidayAddRequest(
    long CalendarId = 0,
    string? HolidayDate = null,
    string? Name = null,
    string? Code = null,
    bool ConfirmWrites = false);

public sealed record ErpOaHolidayAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CalendarId, string? HolidayDate,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "oa_holiday_add", calendar_id = CalendarId, holiday_date = HolidayDate },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
