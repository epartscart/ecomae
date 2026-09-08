namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pf_case_act</c> / <c>epc_pf_case_act</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpPfCaseActWriteService</c>.
/// </summary>
public interface IErpPfCaseActDryRun
{
    ErpPfCaseActDryRunResult Evaluate(ErpPfCaseActRequest request);
}

public sealed class ErpPfCaseActDryRun : IErpPfCaseActDryRun
{
    public ErpPfCaseActDryRunResult Evaluate(ErpPfCaseActRequest request)
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

        if (request.CaseId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Case not found", request);
        }

        return new ErpPfCaseActDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.CaseId, string.IsNullOrWhiteSpace(request.Decision) ? "approve" : request.Decision,
            ["UPDATE `epc_pf_cases` + `epc_pf_case_steps` (NOT executed)"],
            "ErpPfCaseAct payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_processflow.php");
    }

    private static ErpPfCaseActDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPfCaseActRequest request) =>
        new(status, 0, true, false, false, code, false, request.CaseId, request.Decision ?? string.Empty, [], detail,
            "content/shop/finance/epc_erp_processflow.php");
}

public sealed record ErpPfCaseActRequest(
    long CaseId = 0,
    string? Decision = null,
    bool ConfirmWrites = false);

public sealed record ErpPfCaseActDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long CaseId, string Decision,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { id = CaseId, decision = Decision, action = "pf_case_act" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
