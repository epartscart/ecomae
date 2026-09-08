namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_compliance_save_retention</c> /
/// <c>epc_bos_retention_save</c> when <c>confirmWrites</c> is omitted.
/// Live UPSERT is <c>IErpBosRetentionSaveWriteService</c>.
/// </summary>
public interface IErpBosComplianceSaveRetentionDryRun
{
    ErpBosComplianceSaveRetentionDryRunResult Evaluate(ErpBosComplianceSaveRetentionRequest request);
}

public sealed class ErpBosComplianceSaveRetentionDryRun : IErpBosComplianceSaveRetentionDryRun
{
    public ErpBosComplianceSaveRetentionDryRunResult Evaluate(ErpBosComplianceSaveRetentionRequest request)
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

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Refuse("dry-run-invalid", "invalid_request", "Label required", request);
        }

        return new ErpBosComplianceSaveRetentionDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true,
            request.Label, request.DocType,
            ["INSERT `epc_bos_retention_rules` (NOT executed)"],
            "ErpBosRetentionSave payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_bos_compliance.php");
    }

    private static ErpBosComplianceSaveRetentionDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpBosComplianceSaveRetentionRequest request) =>
        new(status, 0, true, false, false, code, false, request.Label, request.DocType, [], detail,
            "content/shop/finance/epc_bos_compliance.php");
}

public sealed record ErpBosComplianceSaveRetentionRequest(
    string? Label = null,
    string? DocType = null,
    bool ConfirmWrites = false);

public sealed record ErpBosComplianceSaveRetentionDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Label, string? DocType,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { label = Label, docType = DocType, action = "bos_compliance_save_retention" },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
