namespace EcomAE.Platform.Migration;

/// <summary>
/// Generic Wave B dry-run for any PHP ajax_erp.php action in the catalog long tail.
/// Never UPDATE/INSERT/DELETE. PHP remains authoritative. confirm_writes always refused.
/// </summary>
public interface IErpAjaxWriteRegistryDryRun
{
    ErpAjaxWriteRegistryDryRunResult Evaluate(ErpAjaxWriteRegistryRequest request);
}

public sealed class ErpAjaxWriteRegistryDryRun : IErpAjaxWriteRegistryDryRun
{
    private readonly IErpAjaxWriteCatalog _catalog;

    public ErpAjaxWriteRegistryDryRun(IErpAjaxWriteCatalog catalog) => _catalog = catalog;

    public ErpAjaxWriteRegistryDryRunResult Evaluate(ErpAjaxWriteRegistryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var action = (request.Action ?? "").Trim();
        if (string.IsNullOrWhiteSpace(action))
            return Refuse("dry-run-invalid", "invalid_request", "action is required.", action, request.ConfirmWrites);

        if (!_catalog.TryGet(action, out var entry))
            return Refuse("dry-run-unknown-action", "unknown_action", $"action '{action}' is not in ajax_erp.php catalog.", action, request.ConfirmWrites);

        if (request.ConfirmWrites)
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                $"confirm_writes requested but live ASP.NET {action} is not implemented; PHP ajax_erp.php remains authoritative.",
                action,
                request.ConfirmWrites,
                entry);

        return new(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: entry.Coverage != "read",
            Action: action,
            Coverage: entry.Coverage,
            AspNetRouteHint: entry.AspNetRouteHint,
            SimulatedSql: [$"ajax_erp.php?action={action} (NOT executed)"],
            Detail: $"ERP ajax action '{action}' catalogued under {entry.Coverage} coverage; UPDATE blocked.",
            PhpAjax: $"/CP/content/shop/finance/erp/ajax_erp.php?action={action}");
    }

    private static ErpAjaxWriteRegistryDryRunResult Refuse(
        string status, string code, string detail, string action, bool confirm, ErpAjaxWriteCatalogEntry? entry = null) =>
        new(status, 0, true, false, true, code, false, action, entry?.Coverage ?? "unknown",
            entry?.AspNetRouteHint ?? "/erp/ajax-writes/dry-run/{action}", [], detail,
            string.IsNullOrEmpty(action)
                ? "/CP/content/shop/finance/erp/ajax_erp.php"
                : $"/CP/content/shop/finance/erp/ajax_erp.php?action={action}");
}

public sealed record ErpAjaxWriteRegistryRequest(string Action, bool ConfirmWrites = false);

public sealed record ErpAjaxWriteRegistryDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string Action, string Coverage, string AspNetRouteHint,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { action = Action, coverage = Coverage, aspNetRouteHint = AspNetRouteHint },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
