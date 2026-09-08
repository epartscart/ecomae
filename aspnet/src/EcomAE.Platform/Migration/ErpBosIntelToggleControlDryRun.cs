namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>bos_intel_toggle_control</c> / <c>epc_bos_intel_set_control</c>
/// when <c>confirmWrites</c> is omitted. Live UPSERT is
/// <c>IErpBosIntelToggleWriteService</c>.
/// </summary>
public interface IErpBosIntelToggleControlDryRun
{
    ErpBosIntelToggleControlDryRunResult Evaluate(ErpBosIntelToggleControlRequest request);
}

public sealed class ErpBosIntelToggleControlDryRun : IErpBosIntelToggleControlDryRun
{
    public ErpBosIntelToggleControlDryRunResult Evaluate(ErpBosIntelToggleControlRequest request)
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

        return new ErpBosIntelToggleControlDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ControlKey, request.Enabled,
            ["ajax_erp.php?action=bos_intel_toggle_control (NOT executed)"],
            "ErpBosIntelToggle payload validated; write blocked until confirmWrites=true.",
            "/CP/content/shop/finance/erp/ajax_erp.php?action=bos_intel_toggle_control");
    }

    private static ErpBosIntelToggleControlDryRunResult Refuse(string s, string c, string d, ErpBosIntelToggleControlRequest r) =>
        new(s, 0, true, false, false, c, false, r.ControlKey, r.Enabled, [], d, "/CP/content/shop/finance/erp/ajax_erp.php?action=bos_intel_toggle_control");
}

public sealed record ErpBosIntelToggleControlRequest(string? ControlKey = null, bool Enabled = true, bool ConfirmWrites = false);

public sealed record ErpBosIntelToggleControlDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? ControlKey, bool Enabled,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "bos_intel_toggle_control", controlKey = ControlKey, enabled = Enabled },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
