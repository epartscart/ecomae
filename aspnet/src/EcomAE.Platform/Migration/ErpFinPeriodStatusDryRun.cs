namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>fin_period_status</c> / <c>epc_fin_period_set_status</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpFinPeriodStatusWriteService</c>.
/// </summary>
public interface IErpFinPeriodStatusDryRun
{
    ErpFinPeriodStatusDryRunResult Evaluate(ErpFinPeriodStatusRequest request);
}

public sealed class ErpFinPeriodStatusDryRun : IErpFinPeriodStatusDryRun
{
    public ErpFinPeriodStatusDryRunResult Evaluate(ErpFinPeriodStatusRequest request)
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

        if (request.Fy <= 0 || request.PeriodNo <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "fy and periodNo must be positive.", request);
        }

        var status = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        if (status.Length == 0)
        {
            return Refuse("dry-run-invalid", "status_required", "status is required.", request);
        }

        if (EcomAE.Platform.Erp.ErpFinPeriodStatusWriteService.NormalizeStatus(status) is null)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Invalid period status", request);
        }

        return new ErpFinPeriodStatusDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.CompanyId, request.Fy, request.PeriodNo, status,
            ["UPDATE `epc_fin_periods` SET `status` (NOT executed)"],
            "ErpFinPeriodStatus payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_fin_advanced.php");
    }

    private static ErpFinPeriodStatusDryRunResult Refuse(string status, string code, string detail, ErpFinPeriodStatusRequest request) =>
        new(status, 0, true, false, false, code, false, request.CompanyId, request.Fy, request.PeriodNo, request.Status, [], detail,
            "content/shop/finance/epc_erp_fin_advanced.php");
}

public sealed record ErpFinPeriodStatusRequest(
    int Fy,
    int PeriodNo,
    string? Status = "open",
    bool ConfirmWrites = false,
    long CompanyId = 0);

public sealed record ErpFinPeriodStatusDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CompanyId, int Fy, int PeriodNo, string? PeriodStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { company_id = CompanyId, fy = Fy, period_no = PeriodNo, status = PeriodStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
