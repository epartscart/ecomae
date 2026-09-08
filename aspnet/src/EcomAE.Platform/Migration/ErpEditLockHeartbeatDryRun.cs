namespace EcomAE.Platform.Migration;

/// <summary>
/// Dry-run envelope for PHP <c>edit_lock_heartbeat</c> / <c>epc_erp_edit_lock_heartbeat</c>
/// when <c>confirmWrites</c> is omitted. Live UPDATE is
/// <c>IErpEditLockHeartbeatWriteService</c>.
/// </summary>
public interface IErpEditLockHeartbeatDryRun
{
    ErpEditLockHeartbeatDryRunResult Evaluate(ErpEditLockHeartbeatRequest request);
}

public sealed class ErpEditLockHeartbeatDryRun : IErpEditLockHeartbeatDryRun
{
    public ErpEditLockHeartbeatDryRunResult Evaluate(ErpEditLockHeartbeatRequest request)
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

        if (string.IsNullOrWhiteSpace(request.ResourceKey))
        {
            return Refuse("dry-run-invalid", "invalid_request", "resourceKey is required.", request);
        }

        return new ErpEditLockHeartbeatDryRunResult(
            "dry-run-validated", 0, true, false, false, "ok", true, request.ResourceKey,
            ["UPDATE `epc_erp_edit_locks` heartbeat (NOT executed)"],
            "ErpEditLockHeartbeat payload validated; write blocked until confirmWrites=true.",
            "content/shop/finance/epc_erp_concurrency.php");
    }

    private static ErpEditLockHeartbeatDryRunResult Refuse(string s, string c, string d, ErpEditLockHeartbeatRequest r) =>
        new(s, 0, true, false, false, c, false, r.ResourceKey, [], d, "content/shop/finance/epc_erp_concurrency.php");
}

public sealed record ErpEditLockHeartbeatRequest(
    string? ResourceKey = null,
    bool ConfirmWrites = false,
    string? EntityType = null,
    string? EntityId = null,
    string? LockToken = null,
    int Ttl = 120);

public sealed record ErpEditLockHeartbeatDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, string? ResourceKey,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new
    {
        ok = true, surface = "erp", status = Status, writes = Writes, writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode, would_write = WouldWrite,
        intended = new { action = "edit_lock_heartbeat", resourceKey = ResourceKey },
        simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail
    };
}
