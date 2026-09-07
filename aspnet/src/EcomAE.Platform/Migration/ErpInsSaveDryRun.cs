namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>ins_save</c> / <c>epc_ins_save</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT/UPDATE is
/// <c>IErpInsSaveWriteService</c>.
/// </summary>
public interface IErpInsSaveDryRun
{
    ErpInsSaveDryRunResult Evaluate(ErpInsSaveRequest request);
}

public sealed class ErpInsSaveDryRun : IErpInsSaveDryRun
{
    public ErpInsSaveDryRunResult Evaluate(ErpInsSaveRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "A policy id must be >= 0.", request);
        }

        var policyNo = (request.PolicyNo ?? string.Empty).Trim();
        if (policyNo.Length == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Policy number is required", request);
        }

        var expiry = (request.ExpiryDate ?? string.Empty).Trim();
        if (expiry.Length == 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Expiry date is required", request);
        }

        return new ErpInsSaveDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Id, request.PolicyNo,
            [request.Id > 0
                ? "UPDATE `epc_erp_ins_policies` (NOT executed)"
                : "INSERT `epc_erp_ins_policies` (NOT executed)"],
            "ErpInsSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_insurance.php");
    }

    private static ErpInsSaveDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpInsSaveRequest request) =>
        new(status, 0, true, false, false, code, false, request.Id, request.PolicyNo, [], detail,
            "content/shop/finance/epc_erp_insurance.php");
}

public sealed record ErpInsSaveRequest(
    long Id = 0,
    string? PolicyNo = null,
    string? ExpiryDate = null,
    bool ConfirmWrites = false);

public sealed record ErpInsSaveDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? PolicyNo,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = Id, policyNo = PolicyNo, action = "ins_save" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
