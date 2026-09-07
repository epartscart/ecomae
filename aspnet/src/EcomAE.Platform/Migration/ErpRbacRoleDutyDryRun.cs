namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rbac_role_duty</c> / <c>epc_rbac_role_attach_duty</c>
/// when <c>confirmWrites</c> is omitted. Live attach/detach is
/// <c>IErpRbacRoleDutyWriteService</c>.
/// </summary>
public interface IErpRbacRoleDutyDryRun
{
    ErpRbacRoleDutyDryRunResult Evaluate(ErpRbacRoleDutyRequest request);
}

public sealed class ErpRbacRoleDutyDryRun : IErpRbacRoleDutyDryRun
{
    public ErpRbacRoleDutyDryRunResult Evaluate(ErpRbacRoleDutyRequest request)
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

        return new ErpRbacRoleDutyDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.RoleId, request.DutyId,
            ["INSERT IGNORE / DELETE `epc_rbac_role_duty` (NOT executed)"],
            "ErpRbacRoleDuty payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_rbac.php");
    }

    private static ErpRbacRoleDutyDryRunResult Refuse(string s, string c, string d, ErpRbacRoleDutyRequest r) =>
        new(s, 0, true, false, false, c, false, r.RoleId, r.DutyId, [], d, "content/shop/finance/epc_erp_rbac.php");
}

public sealed record ErpRbacRoleDutyRequest(
    long RoleId = 0,
    long DutyId = 0,
    int? Attach = null,
    bool ConfirmWrites = false);

public sealed record ErpRbacRoleDutyDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long RoleId, long DutyId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rbac_role_duty", role_id = RoleId, duty_id = DutyId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
