namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rbac_user_role</c> / <c>epc_rbac_user_assign_role</c>
/// when <c>confirmWrites</c> is omitted. Live assign/unassign is
/// <c>IErpRbacUserRoleWriteService</c>.
/// </summary>
public interface IErpRbacUserRoleDryRun
{
    ErpRbacUserRoleDryRunResult Evaluate(ErpRbacUserRoleRequest request);
}

public sealed class ErpRbacUserRoleDryRun : IErpRbacUserRoleDryRun
{
    public ErpRbacUserRoleDryRunResult Evaluate(ErpRbacUserRoleRequest request)
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

        return new ErpRbacUserRoleDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.UserId, request.RoleId,
            ["INSERT IGNORE / DELETE `epc_rbac_user_role` (NOT executed)"],
            "ErpRbacUserRole payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_rbac.php");
    }

    private static ErpRbacUserRoleDryRunResult Refuse(string s, string c, string d, ErpRbacUserRoleRequest r) =>
        new(s, 0, true, false, false, c, false, r.UserId, r.RoleId, [], d, "content/shop/finance/epc_erp_rbac.php");
}

public sealed record ErpRbacUserRoleRequest(
    long CompanyId = 0,
    long UserId = 0,
    long RoleId = 0,
    int? Assign = null,
    bool ConfirmWrites = false);

public sealed record ErpRbacUserRoleDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long UserId, long RoleId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rbac_user_role", user_id = UserId, role_id = RoleId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
