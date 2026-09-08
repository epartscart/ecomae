namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>opl_set_status</c> / <c>epc_opl_set_status</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpOplSetStatusWriteService</c>.
/// </summary>
public interface IErpOplSetStatusDryRun
{
    ErpOplSetStatusDryRunResult Evaluate(ErpOplSetStatusRequest request);
}

public sealed class ErpOplSetStatusDryRun : IErpOplSetStatusDryRun
{
    public ErpOplSetStatusDryRunResult Evaluate(ErpOplSetStatusRequest request)
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

        return new ErpOplSetStatusDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.TargetStatus,
            ["ajax_erp.php?action=opl_set_status (NOT executed)"],
            "ErpOplSetStatus payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=opl_set_status");
    }

    private static ErpOplSetStatusDryRunResult Refuse(string s, string c, string d, ErpOplSetStatusRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.TargetStatus, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=opl_set_status");
}

public sealed record ErpOplSetStatusRequest(long Id = 0, string? TargetStatus = null, bool ConfirmWrites = false);

public sealed record ErpOplSetStatusDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? TargetStatus,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "opl_set_status", id = Id, status = TargetStatus },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
