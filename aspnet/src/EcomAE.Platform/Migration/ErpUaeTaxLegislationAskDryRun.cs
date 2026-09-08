namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>uae_tax_legislation_ask</c> /
/// <c>epc_uae_tax_legislation_ask</c> when <c>confirmWrites</c> is omitted.
/// Live ask is <c>IErpUaeTaxLegislationAskWriteService</c>.
/// </summary>
public interface IErpUaeTaxLegislationAskDryRun
{
    ErpUaeTaxLegislationAskDryRunResult Evaluate(ErpUaeTaxLegislationAskRequest request);
}

public sealed class ErpUaeTaxLegislationAskDryRun : IErpUaeTaxLegislationAskDryRun
{
    public ErpUaeTaxLegislationAskDryRunResult Evaluate(ErpUaeTaxLegislationAskRequest request)
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
            request.Question,
            ["ajax_erp.php?action=uae_tax_legislation_ask (NOT executed)"],
            "ERP uae_tax_legislation_ask payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_legislation_ask");
    }

    private static ErpUaeTaxLegislationAskDryRunResult Refuse(string s, string c, string d, ErpUaeTaxLegislationAskRequest r) =>
        new(s, 0, true, false, false, c, false, r.Id, r.Code, r.Question, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_legislation_ask");
}

public sealed record ErpUaeTaxLegislationAskRequest(
    long Id = 0,
    string? Code = null,
    bool ConfirmWrites = false,
    string? Question = null);

public sealed record ErpUaeTaxLegislationAskDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    long Id,
    string? Code,
    string? Question,
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
        intended = new { id = Id, code = Code, question = Question, action = "uae_tax_legislation_ask" },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail,
    };
}
