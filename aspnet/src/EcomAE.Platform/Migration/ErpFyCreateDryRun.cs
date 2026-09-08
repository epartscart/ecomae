namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>fy_create</c> / <c>epc_fy_create_year</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpFyCreateWriteService</c>.
/// </summary>
public interface IErpFyCreateDryRun
{
    ErpFyCreateDryRunResult Evaluate(ErpFyCreateRequest request);
}

public sealed class ErpFyCreateDryRun : IErpFyCreateDryRun
{
    public ErpFyCreateDryRunResult Evaluate(ErpFyCreateRequest request)
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

        if (request.StartDate <= 0 || request.EndDate <= 0 || request.EndDate < request.StartDate)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Valid start and end dates are required", request);
        }

        return new ErpFyCreateDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Label, request.StartDate, request.EndDate, request.Monthly,
            ["INSERT `epc_fy_years` + `epc_fy_periods` (NOT executed)"],
            "ErpFyCreate payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_closing.php");
    }

    private static ErpFyCreateDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpFyCreateRequest request) =>
        new(status, 0, true, false, false, code, false, request.Label, request.StartDate, request.EndDate, request.Monthly, [], detail,
            "content/shop/finance/epc_erp_closing.php");
}

public sealed record ErpFyCreateRequest(
    string? Label = null,
    long StartDate = 0,
    long EndDate = 0,
    bool Monthly = false,
    bool ConfirmWrites = false);

public sealed record ErpFyCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Label, long StartDate, long EndDate, bool Monthly,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { label = Label, startDate = StartDate, endDate = EndDate, monthly = Monthly, action = "fy_create" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
