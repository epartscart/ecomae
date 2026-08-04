namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave B dry-run for customer path of PHP <c>ajax_send_message.php</c> (order message INSERT).
/// Manager/return_id branches and staff notify stay PHP-authoritative. Never executes writes.
/// </summary>
public interface IStorefrontOrderSendMessageDryRun
{
    Task<StorefrontOrderSendMessageDryRunResult> EvaluateAsync(
        int userId,
        StorefrontOrderSendMessageRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontOrderSendMessageDryRun : IStorefrontOrderSendMessageDryRun
{
    private readonly ISurfaceDashboardSummaryReporter _dashboards;

    public StorefrontOrderSendMessageDryRun(ISurfaceDashboardSummaryReporter dashboards) => _dashboards = dashboards;

    public async Task<StorefrontOrderSendMessageDryRunResult> EvaluateAsync(
        int userId,
        StorefrontOrderSendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET storefront order message is not implemented; PHP ajax_send_message.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Refuse("dry-run-invalid", "message_text_required",
                "Message text is required.", request);
        }

        var orders = await _dashboards.ListStorefrontOrdersAsync(userId, 100, cancellationToken);
        return EvaluateAgainstOrders(userId, orders.Orders, request with { Text = text });
    }

    public static StorefrontOrderSendMessageDryRunResult EvaluateAgainstOrders(
        int userId,
        IReadOnlyList<StorefrontOrderDigest> orders,
        StorefrontOrderSendMessageRequest request)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConfirmWrites)
        {
            return Refuse("dry-run-confirm-refused", "confirm_writes_refused",
                "confirm_writes requested but live ASP.NET storefront order message is not implemented; PHP ajax_send_message.php remains authoritative.",
                request);
        }

        if (userId <= 0)
        {
            return Refuse("dry-run-invalid", "customer_required",
                "Customer session required (PHP DP_User::getUserId).", request);
        }

        if (request.OrderId <= 0)
        {
            return Refuse("dry-run-invalid", "invalid_request", "orderId must be positive.", request);
        }

        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Refuse("dry-run-invalid", "message_text_required",
                "Message text is required.", request);
        }

        var order = orders.FirstOrDefault(o => o.Id == request.OrderId);
        if (order is null)
        {
            return Refuse("dry-run-invalid", "order_not_owned",
                $"Order {request.OrderId} not in customer orders digest (PHP Forbidden).", request);
        }

        var preview = text.Length > 200 ? text[..200] + "…" : text;

        return new StorefrontOrderSendMessageDryRunResult(
            Status: "dry-run-validated",
            Writes: 0,
            WritesBlocked: true,
            CutoverAllowed: false,
            PhpAuthoritative: true,
            ValidationCode: "ok",
            WouldWrite: true,
            OrderId: order.Id,
            UserId: userId,
            TextPreview: preview,
            OrderStatus: order.Status,
            OrderPaid: order.Paid,
            SimulatedSql:
            [
                "INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (@orderId, 1, @text, @now, 0) (NOT executed)",
                "Staff notify (epc_staff_send_notify order_message_to_manager) remains PHP-only in this dry-run slice",
                "Manager path + return_id RMA branch remain PHP-only in this dry-run slice"
            ],
            Detail: "Owned order found in digest window; customer message INSERT simulated. Notify stays PHP until dual-sample.",
            PhpAjax: "/content/shop/messager/ajax_send_message.php");
    }

    private static StorefrontOrderSendMessageDryRunResult Refuse(
        string status, string code, string detail, StorefrontOrderSendMessageRequest request) =>
        new(status, 0, true, false, true, code, false, request.OrderId, 0,
            request.Text, null, null, [], detail,
            "/content/shop/messager/ajax_send_message.php");
}

public sealed record StorefrontOrderSendMessageRequest(
    long OrderId,
    string? Text,
    bool ConfirmWrites = false);

public sealed record StorefrontOrderSendMessageDryRunResult(
    string Status, int Writes, bool WritesBlocked, bool CutoverAllowed, bool PhpAuthoritative,
    string ValidationCode, bool WouldWrite, long OrderId, int UserId, string? TextPreview,
    int? OrderStatus, int? OrderPaid, IReadOnlyList<string> SimulatedSql, string Detail, string PhpAjax)
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
        intended = new { order_id = OrderId, text = TextPreview },
        current = OrderStatus is null ? null : new { status = OrderStatus, paid = OrderPaid, user_id = UserId },
        simulated = SimulatedSql,
        php_ajax = PhpAjax,
        session,
        note = Detail
    };
}
