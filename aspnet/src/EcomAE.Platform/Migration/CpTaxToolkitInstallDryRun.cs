namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>epc_tax_toolkit_install</c> when <c>confirmWrites</c> is omitted.
/// Live install is <c>ICpTaxToolkitWriteService</c>.
/// </summary>
public interface ICpTaxToolkitInstallDryRun
{
    CpTaxToolkitInstallDryRunResult Evaluate(CpTaxToolkitInstallRequest request);
}

public sealed class CpTaxToolkitInstallDryRun : ICpTaxToolkitInstallDryRun
{
    public CpTaxToolkitInstallDryRunResult Evaluate(CpTaxToolkitInstallRequest request)
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
                request.KitCode,
                request.SetDefault,
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
            request.KitCode,
            request.SetDefault,
            ["epc_tax_toolkit_install (NOT executed)"],
            "CP tax toolkit install payload validated; write blocked until confirmWrites=true.",
            "/CP/control/portal/epc_tax_toolkit_manage");
    }
}

public sealed record CpTaxToolkitInstallRequest(
    string? KitCode = null,
    bool SetDefault = false,
    bool ConfirmWrites = false);

public sealed record CpTaxToolkitInstallDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    string? KitCode,
    bool SetDefault,
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
        intended = new { action = "install", kit_code = KitCode, set_default = SetDefault },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail,
    };
}
