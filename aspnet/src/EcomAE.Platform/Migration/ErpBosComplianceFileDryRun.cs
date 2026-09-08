namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_compliance_file</c> /
/// <c>epc_bos_compliance_set_filing</c> when <c>confirmWrites</c> is omitted.
/// Live UPSERT is <c>IErpBosComplianceFileWriteService</c>.
/// </summary>
public interface IErpBosComplianceFileDryRun
{
    ErpBosComplianceFileDryRunResult Evaluate(ErpBosComplianceFileRequest request);
}

public sealed class ErpBosComplianceFileDryRun : IErpBosComplianceFileDryRun
{
    public ErpBosComplianceFileDryRunResult Evaluate(ErpBosComplianceFileRequest request)
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

        if (request.ObligationId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "Select an obligation", request);
        }

        if (string.IsNullOrWhiteSpace(request.PeriodLabel))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Period label is required", request);
        }

        return new ErpBosComplianceFileDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.ObligationId, request.PeriodLabel,
            ["INSERT `epc_bos_compliance_filings` (NOT executed)"],
            "ErpBosComplianceFile payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_compliance.php");
    }

    private static ErpBosComplianceFileDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpBosComplianceFileRequest request) =>
        new(status, 0, true, false, false, code, false, request.ObligationId, request.PeriodLabel, [], detail,
            "content/shop/finance/epc_bos_compliance.php");
}

public sealed record ErpBosComplianceFileRequest(
    long ObligationId = 0,
    string? PeriodLabel = null,
    bool ConfirmWrites = false);

public sealed record ErpBosComplianceFileDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long ObligationId, string? PeriodLabel,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { obligationId = ObligationId, periodLabel = PeriodLabel, action = "bos_compliance_file" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
