namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>rbac_duty_priv</c> / <c>epc_rbac_duty_attach_priv</c>
/// when <c>confirmWrites</c> is omitted. Live attach/detach is
/// <c>IErpRbacDutyPrivWriteService</c>.
/// </summary>
public interface IErpRbacDutyPrivDryRun
{
    ErpRbacDutyPrivDryRunResult Evaluate(ErpRbacDutyPrivRequest request);
}

public sealed class ErpRbacDutyPrivDryRun : IErpRbacDutyPrivDryRun
{
    public ErpRbacDutyPrivDryRunResult Evaluate(ErpRbacDutyPrivRequest request)
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

        return new ErpRbacDutyPrivDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.DutyId, request.PrivilegeId,
            ["INSERT IGNORE / DELETE `epc_rbac_duty_priv` (NOT executed)"],
            "ErpRbacDutyPriv payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_rbac.php");
    }

    private static ErpRbacDutyPrivDryRunResult Refuse(string s, string c, string d, ErpRbacDutyPrivRequest r) =>
        new(s, 0, true, false, false, c, false, r.DutyId, r.PrivilegeId, [], d, "content/shop/finance/epc_erp_rbac.php");
}

public sealed record ErpRbacDutyPrivRequest(
    long DutyId = 0,
    long PrivilegeId = 0,
    int? Attach = null,
    bool ConfirmWrites = false);

public sealed record ErpRbacDutyPrivDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long DutyId, long PrivilegeId,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "rbac_duty_priv", duty_id = DutyId, privilege_id = PrivilegeId },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
