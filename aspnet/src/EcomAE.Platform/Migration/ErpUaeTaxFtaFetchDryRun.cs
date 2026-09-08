namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>uae_tax_fta_fetch</c> /
/// <c>epc_uae_fta_fetch_legislation_updates</c> when <c>confirmWrites</c> is omitted.
/// Live fetch is <c>IErpUaeTaxFtaFetchWriteService</c>.
/// </summary>
public interface IErpUaeTaxFtaFetchDryRun
{
    ErpUaeTaxFtaFetchDryRunResult Evaluate(ErpUaeTaxFtaFetchRequest request);
}

public sealed class ErpUaeTaxFtaFetchDryRun : IErpUaeTaxFtaFetchDryRun
{
    public ErpUaeTaxFtaFetchDryRunResult Evaluate(ErpUaeTaxFtaFetchRequest request)
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
            ["ajax_erp.php?action=uae_tax_fta_fetch (NOT executed)"],
            "ERP uae_tax_fta_fetch payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_fta_fetch");
    }

    private static ErpUaeTaxFtaFetchDryRunResult Refuse(string s, string c, string d, ErpUaeTaxFtaFetchRequest r) =>
        new(s, 0, true, false, false, c, false, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=uae_tax_fta_fetch");
}

public sealed record ErpUaeTaxFtaFetchRequest(bool ConfirmWrites = false, bool Force = false);

public sealed record ErpUaeTaxFtaFetchDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
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
        intended = new { action = "uae_tax_fta_fetch" },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail,
    };
}
