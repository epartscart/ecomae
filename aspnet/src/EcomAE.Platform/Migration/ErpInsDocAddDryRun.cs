namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>ins_doc_add</c> / <c>epc_ins_doc_add</c> when
/// <c>confirmWrites</c> is omitted. Live INSERT is <c>IErpInsDocAddWriteService</c>.
/// </summary>
public interface IErpInsDocAddDryRun
{
    ErpInsDocAddDryRunResult Evaluate(ErpInsDocAddRequest request);
}

public sealed class ErpInsDocAddDryRun : IErpInsDocAddDryRun
{
    public ErpInsDocAddDryRunResult Evaluate(ErpInsDocAddRequest request)
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

        if (request.PolicyId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select a policy", request);
        }

        return new ErpInsDocAddDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.PolicyId, request.Title,
            ["INSERT `epc_erp_ins_documents` (NOT executed)"],
            "ErpInsDocAdd payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_insurance.php");
    }

    private static ErpInsDocAddDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpInsDocAddRequest request) =>
        new(status, 0, true, false, false, code, false, request.PolicyId, request.Title, [], detail,
            "content/shop/finance/epc_erp_insurance.php");
}

public sealed record ErpInsDocAddRequest(
    long PolicyId = 0,
    string? Title = null,
    bool ConfirmWrites = false);

public sealed record ErpInsDocAddDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long PolicyId, string? Title,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { policyId = PolicyId, title = Title, action = "ins_doc_add" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
