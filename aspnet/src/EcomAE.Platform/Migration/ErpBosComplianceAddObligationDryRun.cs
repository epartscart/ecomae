namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_compliance_add_obligation</c> /
/// <c>epc_bos_compliance_add_obligation</c> when <c>confirmWrites</c> is omitted.
/// Live UPSERT is <c>IErpBosComplianceAddObligationWriteService</c>.
/// </summary>
public interface IErpBosComplianceAddObligationDryRun
{
    ErpBosComplianceAddObligationDryRunResult Evaluate(ErpBosComplianceAddObligationRequest request);
}

public sealed class ErpBosComplianceAddObligationDryRun : IErpBosComplianceAddObligationDryRun
{
    public ErpBosComplianceAddObligationDryRunResult Evaluate(ErpBosComplianceAddObligationRequest request)
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
            return Refuse("dry-run-invalid", "invalid_request", "Title required", request);
        }

        return new ErpBosComplianceAddObligationDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Title, request.Code,
            ["INSERT `epc_bos_compliance_obligations` (NOT executed)"],
            "ErpBosComplianceAddObligation payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_compliance.php");
    }

    private static ErpBosComplianceAddObligationDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpBosComplianceAddObligationRequest request) =>
        new(status, 0, true, false, false, code, false, request.Title, request.Code, [], detail,
            "content/shop/finance/epc_bos_compliance.php");
}

public sealed record ErpBosComplianceAddObligationRequest(
    string? Title = null,
    string? Code = null,
    bool ConfirmWrites = false);

public sealed record ErpBosComplianceAddObligationDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Title, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { title = Title, code = Code, action = "bos_compliance_add_obligation" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
