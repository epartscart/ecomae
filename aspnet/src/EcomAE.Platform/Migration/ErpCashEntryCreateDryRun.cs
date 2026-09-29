namespace EcomAE.Platform.Migration;

/// <summary>
/// Validation envelope for the live ASP.NET cash-entry writer.
/// </summary>
public interface IErpCashEntryCreateDryRun
{
    Task<ErpCashEntryCreateDryRunResult> EvaluateAsync(
        ErpCashEntryCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCashEntryCreateDryRun : IErpCashEntryCreateDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public ErpCashEntryCreateDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<ErpCashEntryCreateDryRunResult> EvaluateAsync(
        ErpCashEntryCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "Use confirm_writes=true to execute the live ASP.NET cash-entry writer.",
                request);
        }

        if (request.AccountId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_entry",
                "accountId and amount required (PHP Invalid entry).", request);
        }

        // Touch cash accounts digest so dry-run exercises the same DB gate.
        _ = await _dashboards.ListErpCashAccountsAsync(50, cancellationToken);
        return EvaluateShape(request);
    }

    public static ErpCashEntryCreateDryRunResult EvaluateShape(ErpCashEntryCreateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "Use confirm_writes=true to execute the live ASP.NET cash-entry writer.",
                request);
        }

        if (request.AccountId <= 0 || request.Amount <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_entry",
                "accountId and amount required (PHP Invalid entry).", request);
        }

        var direction = request.Direction ? 1 : 0;
        var entryType = string.IsNullOrWhiteSpace(request.EntryType)
            ? (direction == 1 ? "receipt" : "payment")
            : request.EntryType.Trim();

        return new ErpCashEntryCreateDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: false,
            ValidationCode: "ok",
            WouldWrite: true,
            AccountId: request.AccountId,
            Amount: request.Amount,
            Direction: direction,
            EntryType: entryType,
            SimulatedSql:
            [
                "INSERT INTO `epc_erp_cash_bank_entries` (…) (NOT executed)",
                "GL post / dimension save (NOT executed)"
            ],
            Detail: "Payload shape validated; no write was performed.",
            PhpAjax: "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_entry");
    }

    private static ErpCashEntryCreateDryRunResult Refuse(
        string status, string code, string detail, ErpCashEntryCreateRequest request) =>
        new(status, 0, true, false, false, code, false, request.AccountId, request.Amount,
            request.Direction ? 1 : 0, request.EntryType, [], detail,
            "/CP/content/shop/finance/erp/ajax_erp.php?action=cash_entry");
}

public sealed record ErpCashEntryCreateRequest(
    long AccountId,
    decimal Amount,
    bool Direction = false,
    string? EntryType = null,
    string? Reference = null,
    string? Note = null,
    bool ConfirmWrites = false);

public sealed record ErpCashEntryCreateDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long AccountId, decimal Amount, int Direction,
    string? EntryType, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { account_id = AccountId, amount = Amount, direction = Direction, entry_type = EntryType },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
