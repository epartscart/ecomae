namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_tax_toolkit_assign_tenant</c> when <c>confirmWrites</c> is omitted.
/// Live assign is <c>ICpTaxToolkitWriteService</c>.
/// </summary>
public interface ICpTaxToolkitAssignDryRun
{
    CpTaxToolkitAssignDryRunResult Evaluate(CpTaxToolkitAssignRequest request);
}

public sealed class CpTaxToolkitAssignDryRun : ICpTaxToolkitAssignDryRun
{
    public CpTaxToolkitAssignDryRunResult Evaluate(CpTaxToolkitAssignRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return new(
                "dry-run-confirm-refused",
                0,
                true,
                false,
                false,
                "confirm_writes_refused",
                false,
                request.CountryCode,
                request.KitCode,
                request.SiteKey,
                [],
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write on ASP.NET.",
                "/CP/control/portal/epc_tax_toolkit_manage");
        }

        return new(
            "dry-run-validated",
            0,
            true,
            false,
            false,
            "ok",
            true,
            request.CountryCode,
            request.KitCode,
            request.SiteKey,
            ["epc_tax_toolkit_assign_tenant (NOT executed)"],
            "CP tax toolkit assign payload validated; write blocked until confirmWrites=true.",
            "/CP/control/portal/epc_tax_toolkit_manage");
    }
}

public sealed record CpTaxToolkitAssignRequest(
    string? CountryCode = null,
    string? KitCode = null,
    string? SiteKey = null,
    bool ConfirmWrites = false);

public sealed record CpTaxToolkitAssignDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    string? CountryCode,
    string? KitCode,
    string? SiteKey,
    IReadOnlyList<string> SimulatedSql,
    string Detail,
    string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "cp",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { action = "assign_tenant", country_code = CountryCode, kit_code = KitCode, site_key = SiteKey },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail,
    };
}
