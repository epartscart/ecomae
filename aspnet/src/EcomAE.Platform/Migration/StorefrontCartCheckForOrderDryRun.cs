namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_check_for_order.php</c> (toggle <c>checked_for_order</c>).
/// SELECT-only; never executes UPDATE. PHP remains authoritative.
/// </summary>
public interface IStorefrontCartCheckForOrderDryRun
{
    Task<StorefrontCartCheckForOrderDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartCheckForOrderRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontCartCheckForOrderDryRun : IStorefrontCartCheckForOrderDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontCartCheckForOrderDryRun(ISurfaceDashboardSummaryReporter dashboards)
    {
        _dashboards = dashboards;
    }

    public async Task<StorefrontCartCheckForOrderDryRunResult> EvaluateAsync(
        int userId,
        StorefrontCartCheckForOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET checked_for_order write is not implemented; PHP ajax_check_for_order.php remains authoritative.",
                request,
                []);
        }

        if (userId <= 0 || request.Records.Count == 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "userId and at least one cart record id are required.",
                request,
                []);
        }

        var cart = await _dashboards.ListStorefrontCartAsync(userId, 500, cancellationToken);
        return EvaluateOwnedLines(cart.Lines, request);
    }

    public static StorefrontCartCheckForOrderDryRunResult EvaluateOwnedLines(
        IReadOnlyList<StorefrontCartLineDigest> lines,
        StorefrontCartCheckForOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET checked_for_order write is not implemented; PHP ajax_check_for_order.php remains authoritative.",
                request,
                []);
        }

        if (request.Records.Count == 0)
        {
            return Refuse(
                "dry-run-invalid",
                "invalid_request",
                "records[] required.",
                request,
                []);
        }

        var planned = new List<StorefrontCartCheckForOrderPlannedToggle>();
        foreach (var id in request.Records.Distinct())
        {
            if (id <= 0)
            {
                return Refuse(
                    "dry-run-invalid",
                    "invalid_request",
                    "record ids must be positive.",
                    request,
                    planned);
            }

            var line = lines.FirstOrDefault(l => l.Id == id);
            if (line is null)
            {
                return Refuse(
                    "dry-run-invalid",
                    "cart_item_not_found",
                    $"Cart line {id} not found for authenticated user (session_id=0 ownership gate).",
                    request,
                    planned);
            }

            var next = line.CheckedForOrder ? 0 : 1;
            planned.Add(new StorefrontCartCheckForOrderPlannedToggle(id, line.CheckedForOrder ? 1 : 0, next));
        }

        return new StorefrontCartCheckForOrderDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            Planned: planned,
            SimulatedSql: "UPDATE `shop_carts` SET `checked_for_order` = @checked WHERE `id` = @id (NOT executed — dry-run)",
            Detail: $"Would toggle checked_for_order on {planned.Count} line(s); write remains blocked until dual-sample + approval.",
            PhpAjax: "/content/shop/order_process/ajax_check_for_order.php",
            RequestRecords: request.Records);
    }

    private static StorefrontCartCheckForOrderDryRunResult Refuse(
        string status,
        string validationCode,
        string detail,
        StorefrontCartCheckForOrderRequest request,
        IReadOnlyList<StorefrontCartCheckForOrderPlannedToggle> planned) =>
        new(
            Status: status,
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: validationCode,
            WouldWrite: false,
            Planned: planned,
            SimulatedSql: null,
            Detail: detail,
            PhpAjax: "/content/shop/order_process/ajax_check_for_order.php",
            RequestRecords: request.Records);
}

public sealed record StorefrontCartCheckForOrderRequest(IReadOnlyList<long> Records, bool ConfirmWrites = false);

public sealed record StorefrontCartCheckForOrderPlannedToggle(long CartRecordId, int CurrentChecked, int NextChecked);

public sealed record StorefrontCartCheckForOrderDryRunResult(
    string Status,
    int Writes,
    bool WritesBlocked,
    bool CutoverAllowed,
    bool PhpAuthoritative,
    string ValidationCode,
    bool WouldWrite,
    IReadOnlyList<StorefrontCartCheckForOrderPlannedToggle> Planned,
    string? SimulatedSql,
    string Detail,
    string PhpAjax,
    IReadOnlyList<long> RequestRecords)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "storefront",
        status = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = CutoverAllowed,
        phpAuthoritative = PhpAuthoritative,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        records = Planned.Select(p => new
        {
            cart_record_id = p.CartRecordId,
            checked_for_order_current = p.CurrentChecked,
            checked_for_order_next = p.NextChecked
        }),
        intended = new { records = RequestRecords },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
