namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for PHP <c>ajax_set_orders_viewed.php</c> (viewed_flag UPDATE).
/// Never executes writes. PHP remains authoritative.
/// </summary>
public interface ICpOmsSetViewedDryRun
{
    Task<CpOmsSetViewedDryRunResult> EvaluateAsync(
        CpOmsSetViewedRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CpOmsSetViewedDryRun : ICpOmsSetViewedDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public CpOmsSetViewedDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<CpOmsSetViewedDryRunResult> EvaluateAsync(
        CpOmsSetViewedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set-viewed is not implemented; PHP ajax_set_orders_viewed.php remains authoritative.",
                request);
        }

        if (request.ViewedFlag is not (0 or 1))
        {
            return Refuse("dry-run-invalid", "invalid_viewed_flag",
                "viewedFlag must be 0 or 1.", request);
        }

        var ids = (request.OrderIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Refuse("dry-run-invalid", "orders_required",
                "At least one positive orderId is required.", request);
        }

        var orders = await _dashboards.ListCpOrdersAsync(200, cancellationToken);
        return EvaluateAgainstOrders(orders.Orders, request with { OrderIds = ids });
    }

    public static CpOmsSetViewedDryRunResult EvaluateAgainstOrders(
        IReadOnlyList<CpShopOrderDigest> orders,
        CpOmsSetViewedRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET OMS set-viewed is not implemented; PHP ajax_set_orders_viewed.php remains authoritative.",
                request);
        }

        if (request.ViewedFlag is not (0 or 1))
        {
            return Refuse("dry-run-invalid", "invalid_viewed_flag",
                "viewedFlag must be 0 or 1.", request);
        }

        var ids = (request.OrderIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Refuse("dry-run-invalid", "orders_required",
                "At least one positive orderId is required.", request);
        }

        var known = orders.Select(o => o.Id).ToHashSet();
        var missing = ids.Where(id => !known.Contains(id)).ToArray();
        if (missing.Length > 0)
        {
            return Refuse("dry-run-invalid", "order_not_in_digest_window",
                $"Order(s) {string.Join(",", missing)} not in recent /cp/orders digest window.",
                request with { OrderIds = ids });
        }

        return new CpOmsSetViewedDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderIds: ids,
            ViewedFlag: request.ViewedFlag,
            SimulatedSql:
            [
                $"UPDATE `shop_orders_viewed` SET `viewed_flag`={request.ViewedFlag} WHERE `order_id` IN ({string.Join(",", ids)}) (NOT executed)",
                "CSRF / admin ACL remain PHP-only in this dry-run slice"
            ],
            Detail: $"{ids.Length} order(s) found in digest window; viewed_flag UPDATE simulated.",
            PhpAjax: "/CP/content/shop/order_process/ajax_set_orders_viewed.php");
    }

    private static CpOmsSetViewedDryRunResult Refuse(
        string status, string code, string detail, CpOmsSetViewedRequest request) =>
        new(status, 0, true, false, true, code, false,
            request.OrderIds ?? [], request.ViewedFlag, [], detail,
            "/CP/content/shop/order_process/ajax_set_orders_viewed.php");
}

public sealed record CpOmsSetViewedRequest(
    IReadOnlyList<long>? OrderIds,
    int ViewedFlag,
    bool ConfirmWrites = false);

public sealed record CpOmsSetViewedDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, IReadOnlyList<long> OrderIds, int ViewedFlag,
    IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { order_ids = OrderIds, viewed_flag = ViewedFlag },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
