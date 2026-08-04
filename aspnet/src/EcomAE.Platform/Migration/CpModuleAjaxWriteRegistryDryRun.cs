namespace EcomAE.Platform.Migration;

/// <summary>Generic Wave C dry-run for any CP module ajax action in the catalog. Never UPDATE. PHP authoritative.</summary>
public interface ICpModuleAjaxWriteRegistryDryRun { CpModuleAjaxWriteRegistryDryRunResult Evaluate(CpModuleAjaxWriteRegistryRequest request); }
public sealed class CpModuleAjaxWriteRegistryDryRun : ICpModuleAjaxWriteRegistryDryRun
{
    private readonly ICpModuleAjaxWriteCatalog _catalog;
    public CpModuleAjaxWriteRegistryDryRun(ICpModuleAjaxWriteCatalog catalog) => _catalog = catalog;
    public CpModuleAjaxWriteRegistryDryRunResult Evaluate(CpModuleAjaxWriteRegistryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var module = (request.Module ?? "").Trim();
        var action = (request.Action ?? "").Trim();
        if (string.IsNullOrWhiteSpace(module) || string.IsNullOrWhiteSpace(action))
            return Refuse("dry-run-invalid", "invalid_request", "module and action are required.", module, action);
        if (!_catalog.TryGet(module, action, out var entry))
            return Refuse("dry-run-unknown-action", "unknown_action", $"action '{module}/{action}' is not in CP module ajax catalog.", module, action);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused", $"confirm_writes refused; PHP remains authoritative for {module}/{action}.", module, action, entry);
        return new("dry-run-validated", 0, true, false, true, "ok", true, entry.Module, entry.Action, entry.Coverage, entry.AspNetRouteHint,
            [$"{entry.PhpAjax}?action={entry.Action} (NOT executed)"],
            $"CP module ajax '{entry.Module}/{entry.Action}' catalogued; UPDATE blocked.",
            entry.PhpAjax);
    }
    private static CpModuleAjaxWriteRegistryDryRunResult Refuse(string s, string c, string d, string module, string action, CpModuleAjaxWriteCatalogEntry? entry = null) =>
        new(s, 0, true, false, true, c, false, module, action, entry?.Coverage ?? "unknown",
            entry?.AspNetRouteHint ?? "/cp/module-ajax/dry-run/{module}/{action}", [], d,
            entry?.PhpAjax ?? "cp/content/shop/*/ajax_*.php");
}
public sealed record CpModuleAjaxWriteRegistryRequest(string Module, string Action, bool ConfirmWrites = false);
public sealed record CpModuleAjaxWriteRegistryDryRunResult(string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative, string ValidationCode, bool WouldWrite, string Module, string Action, string Coverage, string AspNetRouteHint, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new { ok = true, surface = "cp", status = Status, writes = Writes, writesBlocked = WritesBlocked, cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative, validation_code = ValidationCode, would_write = WouldWrite, intended = new { module = Module, action = Action, coverage = Coverage, aspNetRouteHint = AspNetRouteHint }, simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail };
}
