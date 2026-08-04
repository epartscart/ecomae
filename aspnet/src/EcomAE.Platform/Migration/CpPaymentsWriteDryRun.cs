namespace EcomAE.Platform.Migration;

/// <summary>Wave B dry-run for PHP <c>cp/content/shop/payments/ajax_payments.php</c>. Never UPDATE. PHP authoritative.</summary>
public interface ICpPaymentsWriteDryRun { CpPaymentsWriteDryRunResult Evaluate(CpPaymentsWriteRequest request); }
public sealed class CpPaymentsWriteDryRun : ICpPaymentsWriteDryRun
{
    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "seed_dummy", "activate", "save_config", "save_account", "disable_account", "seed_platform_account", "mark_settlement",
    };

    public CpPaymentsWriteDryRunResult Evaluate(CpPaymentsWriteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused", "confirm_writes refused; PHP cp/content/shop/payments/ajax_payments.php remains authoritative.", request);
        var action = (request.Action ?? "").Trim();
        if (string.IsNullOrWhiteSpace(action) || !AllowedActions.Contains(action))
            return Refuse("dry-run-unknown-action", "unknown_action", $"action '{action}' is not a known payments ajax action.", request);
        return new("dry-run-validated", 0, true, false, true, "ok", true, action,
            [$"cp/content/shop/payments/ajax_payments.php?action={action} (NOT executed)"],
            "CpPaymentsWrite payload validated; UPDATE blocked.",
            "cp/content/shop/payments/ajax_payments.php");
    }
    private static CpPaymentsWriteDryRunResult Refuse(string s, string c, string d, CpPaymentsWriteRequest r) =>
        new(s, 0, true, false, true, c, false, r.Action, [], d, "cp/content/shop/payments/ajax_payments.php");
}
public sealed record CpPaymentsWriteRequest(string? Action = null, bool ConfirmWrites = false);
public sealed record CpPaymentsWriteDryRunResult(string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative, string ValidationCode, bool WouldWrite, string? Action, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
{
    public object ToPayload(object session) => new { ok = true, surface = "cp", status = Status, writes = Writes, writesBlocked = WritesBlocked, cutoverAllowed = CutoverAllowed, phpAuthoritative = PhpAuthoritative, validation_code = ValidationCode, would_write = WouldWrite, intended = new { action = Action }, simulated = SimulatedSql, php_ajax = PhpAjax, session, note = Detail };
}
