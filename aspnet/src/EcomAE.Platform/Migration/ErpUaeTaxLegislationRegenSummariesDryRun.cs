namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>uae_tax_legislation_regen_summaries</c> /
/// <c>epc_uae_tax_legislation_backfill_summaries</c> when <c>confirmWrites</c> is omitted.
/// Live regen is <c>IErpUaeTaxLegislationRegenWriteService</c>.
/// </summary>
public interface IErpUaeTaxLegislationRegenSummariesDryRun
{
    ErpUaeTaxLegislationRegenSummariesDryRunResult Evaluate(ErpUaeTaxLegislationRegenSummariesRequest request);
}

public sealed class ErpUaeTaxLegislationRegenSummariesDryRun : IErpUaeTaxLegislationRegenSummariesDryRun
{
    public ErpUaeTaxLegislationRegenSummariesDryRunResult Evaluate(ErpUaeTaxLegislationRegenSummariesRequest request)
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

        return new(
            "dry-run-validated",
            0,
            true,
            false,
            false,
            "ok",
            true,
            request.Id,
            request.Code,
            request.FetchPdf,
            ["ajax_erp.php?action=uae_tax_legislation_regen_summaries (NOT executed)"],
            "ERP uae_tax_legislation_regen_summaries payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_legislation_regen_summaries");
    }

    private static ErpUaeTaxLegislationRegenSummariesDryRunResult Refuse(string s, string c, string d, ErpUaeTaxLegislationRegenSummariesRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, r.FetchPdf, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_legislation_regen_summaries");
}

public sealed record ErpUaeTaxLegislationRegenSummariesRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    bool FetchPdf = false);

public sealed record ErpUaeTaxLegislationRegenSummariesDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long Id,
    string? Code,
    bool FetchPdf,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { id = Id, code = Code, fetch_pdf = FetchPdf, action = "uae_tax_legislation_regen_summaries" },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail,
    };
}
