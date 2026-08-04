namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>marketing_create</c>. Never INSERT. PHP authoritative.</summary>
public interface IErpMarketingCreateDryRun
{
    ErpMarketingCreateDryRunResult Evaluate(ErpMarketingCreateRequest request);
}

public sealed class ErpMarketingCreateDryRun : IErpMarketingCreateDryRun
{
    public ErpMarketingCreateDryRunResult Evaluate(ErpMarketingCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET marketing_create is not implemented; PHP ajax_erp.php remains authoritative.", request);

        // PHP defaults empty name to "Campaign".
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            name = "Campaign";
        }

        return new ErpMarketingCreateDryRunResult(
            "dry-run-validated", 0, true, false, true, "ok", true, name,
            ["INSERT INTO `epc_erp_marketing_campaigns` (…) (NOT executed)"],
            "Marketing campaign create payload validated; INSERT blocked.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=marketing_create");
    }

    private static ErpMarketingCreateDryRunResult Refuse(string status, string code, string detail, ErpMarketingCreateRequest request) =>
        new(status, 0, true, false, true, code, false, request.Name, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=marketing_create");
}

public sealed record ErpMarketingCreateRequest(string? Name, bool ConfirmWrites = false);
public sealed record ErpMarketingCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? Name,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { name = Name },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
