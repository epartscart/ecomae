namespace EcomAE.Platform.Migration;

/// <summary>Dedicated Wave C dry-run for classified CP module ajax write actions. Never UPDATE. PHP authoritative.</summary>
public interface ICpModuleAjaxWriteDedicatedDryRun { CpModuleAjaxWriteDedicatedDryRunResult Evaluate(CpModuleAjaxWriteDedicatedRequest request); }
public sealed class CpModuleAjaxWriteDedicatedDryRun : ICpModuleAjaxWriteDedicatedDryRun
{
    private readonly ICpModuleAjaxWriteCatalog _catalog;
    public CpModuleAjaxWriteDedicatedDryRun(ICpModuleAjaxWriteCatalog catalog) => _catalog = catalog;
    public CpModuleAjaxWriteDedicatedDryRunResult Evaluate(CpModuleAjaxWriteDedicatedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var module = (request.Module ?? "").Trim();
        var action = (request.Action ?? "").Trim();
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(action))
            return Refuse("dry-run-invalid", "invalid_request", "module and action are required.", module, action);
        if (!_catalog.TryGet(module, action, out var entry) || !string.Equals(entry.Coverage, "dedicated", StringComparison.OrdinalIgnoreCase))
            return Refuse("dry-run-unknown-action", "unknown_or_non_write_action", $"action '{module}/{action}' is not a dedicated CP module write dry-run. Use registry or GET /cp/module-ajax/writes/catalog.", module, action, entry);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused", $"confirm_writes refused; PHP remains authoritative for {module}/{action}.", module, action, entry);
        return new("dry-run-validated", 0, true, false, true, "ok", true, entry.Module, entry.Action, entry.AspNetRouteHint,
            [$"{entry.PhpAjax}?action={entry.Action} (NOT executed)"],
            $"CP module write '{entry.Module}/{entry.Action}' dedicated dry-run; UPDATE blocked.",
            entry.PhpAjax);
    }
    private static CpModuleAjaxWriteDedicatedDryRunResult Refuse(string s, string c, string d, string module, string action, CpModuleAjaxWriteCatalogEntry? entry = null) =>
        new(s, 0, true, false, true, c, false, module, action, entry?.AspNetRouteHint ?? "/cp/module-ajax/{module}/{action}/dry-run", [], d,
            entry?.PhpAjax ?? "cp/content/shop/*/ajax_*.php");
}
public sealed record CpModuleAjaxWriteDedicatedRequest(string Module, string Action, bool ConfirmWrites = false);
public sealed record CpModuleAjaxWriteDedicatedDryRunResult(string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative, string ValidationCode, bool WouldWrite, string Module, string Action, string AspNetRouteHint, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new { ok = true, surface = "cp", status = Status, writes = Writes, writesBlocked = WritesBlocked, cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative, validation_code = ValidationCode, would_write = WouldWrite, intended = new { module = Module, action = Action, aspNetRouteHint = AspNetRouteHint }, simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail };
}
