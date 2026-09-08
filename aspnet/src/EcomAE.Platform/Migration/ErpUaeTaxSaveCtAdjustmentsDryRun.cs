namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>uae_tax_save_ct_adjustments</c> /
/// <c>epc_uae_ct_save_adjustments</c> when <c>confirmWrites</c> is omitted.
/// Live UPSERT is <c>IErpUaeTaxSaveCtAdjustmentsWriteService</c>.
/// </summary>
public interface IErpUaeTaxSaveCtAdjustmentsDryRun
{
    ErpUaeTaxSaveCtAdjustmentsDryRunResult Evaluate(ErpUaeTaxSaveCtAdjustmentsRequest request);
}

public sealed class ErpUaeTaxSaveCtAdjustmentsDryRun : IErpUaeTaxSaveCtAdjustmentsDryRun
{
    public ErpUaeTaxSaveCtAdjustmentsDryRunResult Evaluate(ErpUaeTaxSaveCtAdjustmentsRequest request)
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

        return new ErpUaeTaxSaveCtAdjustmentsDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.Id, request.Code,
            ["ajax_erp.php?action=uae_tax_save_ct_adjustments (NOT executed)"],
            "ErpUaeTaxSaveCtAdjustments payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_save_ct_adjustments");
    }

    private static ErpUaeTaxSaveCtAdjustmentsDryRunResult Refuse(string s, string c, string d, ErpUaeTaxSaveCtAdjustmentsRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_save_ct_adjustments");
}

public sealed record ErpUaeTaxSaveCtAdjustmentsRequest(long Id = 0, string? Code = null, bool ConfirmWrites = false);

public sealed record ErpUaeTaxSaveCtAdjustmentsDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long Id, string? Code,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "uae_tax_save_ct_adjustments", id = Id, code = Code },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
