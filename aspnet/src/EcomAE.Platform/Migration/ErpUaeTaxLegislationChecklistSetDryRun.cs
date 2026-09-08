namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>uae_tax_legislation_checklist_set</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpUaeTaxLegislationChecklistSetWriteService</c>.
/// </summary>
public interface IErpUaeTaxLegislationChecklistSetDryRun
{
    ErpUaeTaxLegislationChecklistSetDryRunResult Evaluate(ErpUaeTaxLegislationChecklistSetRequest request);
}

public sealed class ErpUaeTaxLegislationChecklistSetDryRun : IErpUaeTaxLegislationChecklistSetDryRun
{
    public ErpUaeTaxLegislationChecklistSetDryRunResult Evaluate(ErpUaeTaxLegislationChecklistSetRequest request)
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

        return new ErpUaeTaxLegislationChecklistSetDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["UPSERT `epc_uae_tax_legislation_checklist` (NOT executed)"],
            "ErpUaeTaxLegislationChecklistSet payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_uae_tax_compliance.php");
    }

    private static ErpUaeTaxLegislationChecklistSetDryRunResult Refuse(string s, string c, string d, ErpUaeTaxLegislationChecklistSetRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "content/shop/finance/epc_uae_tax_compliance.php");
}

public sealed record ErpUaeTaxLegislationChecklistSetRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false);

public sealed record ErpUaeTaxLegislationChecklistSetDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "uae_tax_legislation_checklist_set", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
