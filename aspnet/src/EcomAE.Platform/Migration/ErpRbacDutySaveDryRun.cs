namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rbac_duty_save</c> / <c>epc_rbac_duty_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpRbacDutySaveWriteService</c>.
/// </summary>
public interface IErpRbacDutySaveDryRun
{
    ErpRbacDutySaveDryRunResult Evaluate(ErpRbacDutySaveRequest request);
}

public sealed class ErpRbacDutySaveDryRun : IErpRbacDutySaveDryRun
{
    public ErpRbacDutySaveDryRunResult Evaluate(ErpRbacDutySaveRequest request)
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

        return new ErpRbacDutySaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Code,
            ["INSERT `epc_rbac_duty` ON DUPLICATE KEY UPDATE (NOT executed)"],
            "ErpRbacDutySave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_rbac.php");
    }

    private static ErpRbacDutySaveDryRunResult Refuse(string s, string c, string d, ErpRbacDutySaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Code, [], d, "content/shop/finance/epc_erp_rbac.php");
}

public sealed record ErpRbacDutySaveRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    bool ConfirmWrites = false);

public sealed record ErpRbacDutySaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rbac_duty_save", code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
