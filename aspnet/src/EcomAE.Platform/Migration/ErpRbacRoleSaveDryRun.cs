namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rbac_role_save</c> / <c>epc_rbac_role_save</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpRbacRoleSaveWriteService</c>.
/// </summary>
public interface IErpRbacRoleSaveDryRun
{
    ErpRbacRoleSaveDryRunResult Evaluate(ErpRbacRoleSaveRequest request);
}

public sealed class ErpRbacRoleSaveDryRun : IErpRbacRoleSaveDryRun
{
    public ErpRbacRoleSaveDryRunResult Evaluate(ErpRbacRoleSaveRequest request)
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

        return new ErpRbacRoleSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Code,
            ["INSERT `epc_rbac_role` ON DUPLICATE KEY UPDATE (NOT executed)"],
            "ErpRbacRoleSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_rbac.php");
    }

    private static ErpRbacRoleSaveDryRunResult Refuse(string s, string c, string d, ErpRbacRoleSaveRequest r) =>
        new(s, 0, true, false, false, c, false, r.Code, [], d, "content/shop/finance/epc_erp_rbac.php");
}

public sealed record ErpRbacRoleSaveRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    bool ConfirmWrites = false);

public sealed record ErpRbacRoleSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rbac_role_save", code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
