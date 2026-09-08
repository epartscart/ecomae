namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>pf_case_start</c> / <c>epc_pf_case_start</c>
/// when <c>confirmWrites</c> is omitted. Live INSERT is
/// <c>IErpPfCaseStartWriteService</c>.
/// </summary>
public interface IErpPfCaseStartDryRun
{
    ErpPfCaseStartDryRunResult Evaluate(ErpPfCaseStartRequest request);
}

public sealed class ErpPfCaseStartDryRun : IErpPfCaseStartDryRun
{
    public ErpPfCaseStartDryRunResult Evaluate(ErpPfCaseStartRequest request)
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

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Case title is required", request);
        }

        if (request.ProcessId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "This process has no steps defined yet", request);
        }

        return new ErpPfCaseStartDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.ProcessId, request.Title.Trim(),
            ["INSERT `epc_pf_cases` + `epc_pf_case_steps` (NOT executed)"],
            "ErpPfCaseStart payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_processflow.php");
    }

    private static ErpPfCaseStartDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpPfCaseStartRequest request) =>
        new(status, 0, true, false, false, code, false, request.ProcessId, request.Title ?? string.Empty, [], detail,
            "content/shop/finance/epc_erp_processflow.php");
}

public sealed record ErpPfCaseStartRequest(
    long ProcessId = 0,
    string? Title = null,
    bool ConfirmWrites = false);

public sealed record ErpPfCaseStartDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ProcessId, string Title,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { processId = ProcessId, title = Title, action = "pf_case_start" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
