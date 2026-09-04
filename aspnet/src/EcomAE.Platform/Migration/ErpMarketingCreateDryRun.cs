namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_erp_marketing_create</c> when <c>confirmWrites</c> is omitted.
/// Live INSERT is <c>IErpMarketingWriteService</c>.
/// </summary>
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
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                request);
        }

        // PHP defaults empty name to "Campaign".
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            name = "Campaign";
        }

        return new ErpMarketingCreateDryRunResult(
            "dry-run-validated",
            0,
            true,
            false,
            false,
            "ok",
            true,
            name,
            ["INSERT INTO `epc_erp_marketing_campaigns` (…) (NOT executed)"],
            "ErpMarketingCreate payload validated; INSERT blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_staff.php");
    }

    private static ErpMarketingCreateDryRunResult Refuse(
        string status,
        string code,
        string detail,
        ErpMarketingCreateRequest request) =>
        new(status, 0, true, false, false, code, false, request.Name, [], detail,
            "content/shop/finance/epc_erp_staff.php");
}

public sealed record ErpMarketingCreateRequest(
    string? Name,
    bool ConfirmWrites = false,
    string? Channel = null,
    decimal Budget = 0,
    string? Status = null,
    string? TimeStart = null,
    string? TimeEnd = null,
    string? Notes = null);

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
