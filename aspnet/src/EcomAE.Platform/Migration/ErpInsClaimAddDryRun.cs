namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>ins_claim_add</c> / <c>epc_ins_claim_save</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpInsClaimAddWriteService</c>.
/// </summary>
public interface IErpInsClaimAddDryRun
{
    ErpInsClaimAddDryRunResult Evaluate(ErpInsClaimAddRequest request);
}

public sealed class ErpInsClaimAddDryRun : IErpInsClaimAddDryRun
{
    public ErpInsClaimAddDryRunResult Evaluate(ErpInsClaimAddRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "A claim id must be >= 0.", request);
        }

        return new ErpInsClaimAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.PolicyId, request.ClaimNo,
            [request.Id > 0
                ? "UPDATE `epc_erp_ins_claims` (NOT executed)"
                : "INSERT `epc_erp_ins_claims` (NOT executed)"],
            "ErpInsClaimAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_insurance.php");
    }

    private static ErpInsClaimAddDryRunResult Refuse(string status, string code, string detail, ErpInsClaimAddRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.PolicyId, request.ClaimNo, [], detail,
            "content/shop/finance/epc_erp_insurance.php");
}

public sealed record ErpInsClaimAddRequest(
    long Id = 0,
    long PolicyId = 0,
    string? ClaimNo = null,
    bool ConfirmWrites = false);

public sealed record ErpInsClaimAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, long PolicyId, string? ClaimNo,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, policy_id = PolicyId, claim_no = ClaimNo },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
